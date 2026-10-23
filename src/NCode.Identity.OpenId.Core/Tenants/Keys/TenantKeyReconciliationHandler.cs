#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Handles <see cref="ReconcileServerSettingsCommand"/> by seeding, through a <see cref="TenantChangedCommand"/> with
/// <see cref="TenantChangedCommand.SettingsChanged"/>, every tenant whose effective settings use a configured algorithm
/// that the tenant holds no satisfying key for. The announcement evicts the tenant, seeds it, and tells the other
/// replicas, so the key the server's settings permit is provisioned without waiting for the tenant to be materialized
/// again. A tenant that already holds every key its settings use is not evicted.
/// A tenant that holds no key is left to its first materialization, which seeds it in full.
/// When the command carries the settings it was last reconciled against, a tenant's keys are read only if the tenant
/// uses, under the current settings, a configured algorithm that it did not use under those: a tenant that used every
/// one of them was reconciled with them already. Without them every tenant that uses a configured algorithm is read.
/// </summary>
/// <remarks>
/// The handler checks the invariant from the current state rather than from a change, so it provisions the keys that a
/// change to the server's settings permits, one that a configuration reload applied, and one that a deployment brought,
/// alike; the settings the command carries only narrow the tenants it reads. A tenant that cannot be handled is logged
/// and skipped, and the pass continues: the tenant is seeded again the next time it is materialized. The handler is
/// idempotent, because announcing a tenant that is already seeded provisions nothing.
/// </remarks>
// See ADR-0169 (decision 10), ADR-0219 and ADR-0234.
internal sealed class TenantKeyReconciliationHandler(
    IOptions<OpenIdOptions> optionsAccessor,
    ITenantKeyPolicy tenantKeyPolicy,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IStoreManagerFactory storeManagerFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<TenantKeyReconciliationHandler> logger
) : ICommandHandler<ReconcileServerSettingsCommand>
{
    // The tenants are read in pages, so the pass never holds every tenant's settings at once.
    private const int TenantPageSize = 100;

    private OpenIdTenantKeyProvisioningOptions Options { get; } =
        optionsAccessor.Value.Tenant.KeyProvisioning;

    private ITenantKeyPolicy TenantKeyPolicy { get; } = tenantKeyPolicy;
    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;
    private ILogger<TenantKeyReconciliationHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        ReconcileServerSettingsCommand command,
        CancellationToken cancellationToken
    )
    {
        if (!Options.Enabled)
            return;

        // The startup validator refuses options that cannot be honored, and the seed reports them.
        var resolution = TenantKeyPolicy.ResolveSpecifications(Options);
        if (resolution.Errors.Count > 0)
            return;

        // The mediator is scoped (its handlers' unit of work is), and a reconciliation runs outside any request, so the
        // announcements run in their own scope.
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        var page = new PageRequest { Limit = TenantPageSize };
        do
        {
            // Each page reads its tenants' keys in its own unit of work, so the pass never tracks every tenant's keys.
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var tenantStore = storeManager.GetStore<ITenantStore>();
            var result = await tenantStore.GetSettingsPageAsync(page, cancellationToken);
            foreach (var tenantSettings in result.Items)
            {
                try
                {
                    var lacksKey = await LacksKeyAsync(
                        tenantStore,
                        command,
                        tenantSettings,
                        resolution.Specifications,
                        cancellationToken
                    );
                    if (!lacksKey)
                        continue;

                    var changed = new TenantChangedCommand(
                        tenantSettings.TenantId,
                        SettingsChanged: true
                    );
                    await mediator.SendAsync(changed, cancellationToken);
                }
                catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // One tenant that cannot be handled, say one with a settings document the server's settings reject,
                    // must not stop the pass: the others still need their keys.
                    Logger.TenantKeyReseedFailed(tenantSettings.TenantId, exception);
                }
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);
    }

    private async ValueTask<bool> LacksKeyAsync(
        ITenantStore tenantStore,
        ReconcileServerSettingsCommand command,
        PersistedTenantSettings tenantSettings,
        IReadOnlyList<TenantKeySpecification> specifications,
        CancellationToken cancellationToken
    )
    {
        var settings = EffectiveSettingsResolver.Resolve(
            command.Settings,
            OpenIdSettingScopes.Tenant,
            tenantSettings.Value
        );
        var used = specifications
            .Where(specification =>
                TenantKeyUsage.IsUsed(Options.AlgorithmSettings, settings, specification.Algorithm)
            )
            .ToList();
        if (used.Count == 0)
            return false;

        // A tenant that used every one of these algorithms before the change was reconciled with them already.
        // See ADR-0234.
        if (command.PreviousSettings is { } previousServerSettings)
        {
            var previousSettings = EffectiveSettingsResolver.Resolve(
                previousServerSettings,
                OpenIdSettingScopes.Tenant,
                tenantSettings.Value
            );
            if (
                used.All(specification =>
                    TenantKeyUsage.IsUsed(
                        Options.AlgorithmSettings,
                        previousSettings,
                        specification.Algorithm
                    )
                )
            )
                return false;
        }

        var secrets = await tenantStore.GetSecretsOrDefaultAsync(
            tenantSettings.TenantId,
            cancellationToken
        );
        IReadOnlyCollection<PersistedSecret> held = secrets is null ? [] : secrets.Value;

        // A tenant that holds no key has never been seeded, or is being seeded right now by its first materialization,
        // which seeds every key it uses through the one pipeline; the reconciliation fills only a gap in a seeded tenant,
        // so it never seeds a tenant concurrently with its first request. See ADR-0166 and ADR-0219.
        if (held.Count == 0)
            return false;

        return used.Any(specification =>
            !held.Any(secret => TenantKeyPolicy.IsSatisfiedBy(specification, secret))
        );
    }
}
