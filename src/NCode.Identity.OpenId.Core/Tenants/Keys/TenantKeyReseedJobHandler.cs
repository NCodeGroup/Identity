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
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Mediator;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Executes a <see cref="TenantKeyReseedJob"/> by announcing a settings change for every tenant whose effective
/// algorithm lists newly name one of the job's algorithms. The announcement evicts the tenant, seeds it, and tells the
/// other replicas, so the key the server's change permits is provisioned without waiting for the tenant to be
/// materialized again.
/// </summary>
/// <remarks>
/// A tenant that cannot be announced is logged and skipped, and the pass continues: the tenant is seeded again the next
/// time it is materialized. The handler is idempotent, because announcing a tenant that is already seeded provisions
/// nothing.
/// </remarks>
// See ADR-0169 (decision 10).
internal sealed class TenantKeyReseedJobHandler(
    IOptions<OpenIdOptions> optionsAccessor,
    IEffectiveSettingsResolver effectiveSettingsResolver,
    IStoreManagerFactory storeManagerFactory,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<TenantKeyReseedJobHandler> logger
) : IDispatchJobHandler<TenantKeyReseedJob>
{
    // The tenants are read in pages, so the pass never holds every tenant's settings at once.
    private const int TenantPageSize = 100;

    private OpenIdTenantKeyProvisioningOptions Options { get; } =
        optionsAccessor.Value.Tenant.KeyProvisioning;

    private IEffectiveSettingsResolver EffectiveSettingsResolver { get; } =
        effectiveSettingsResolver;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;
    private ILogger<TenantKeyReseedJobHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        TenantKeyReseedJob job,
        DispatchContext context,
        CancellationToken cancellationToken
    )
    {
        var previousServerSettings = await EffectiveSettingsResolver.ResolveServerAsync(
            job.PreviousServerSettings,
            cancellationToken
        );
        var currentServerSettings = await EffectiveSettingsResolver.ResolveServerAsync(
            job.ServerSettings,
            cancellationToken
        );

        // The mediator is scoped (its handlers' unit of work is), and a job runs outside any request, so the
        // announcements run in their own scope.
        await using var scope = ServiceScopeFactory.CreateAsyncScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var tenantStore = storeManager.GetStore<ITenantStore>();

        var page = new PageRequest { Limit = TenantPageSize };
        do
        {
            var result = await tenantStore.GetSettingsPageAsync(page, cancellationToken);
            foreach (var tenantSettings in result.Items)
            {
                try
                {
                    var previousTenantSettings = EffectiveSettingsResolver.Resolve(
                        previousServerSettings,
                        OpenIdSettingScopes.Tenant,
                        tenantSettings.Value
                    );
                    var currentTenantSettings = EffectiveSettingsResolver.Resolve(
                        currentServerSettings,
                        OpenIdSettingScopes.Tenant,
                        tenantSettings.Value
                    );

                    var isAdded = job.Algorithms.Any(algorithm =>
                        TenantKeyUsage.IsUsed(
                            Options.AlgorithmSettings,
                            currentTenantSettings,
                            algorithm
                        )
                        && !TenantKeyUsage.IsUsed(
                            Options.AlgorithmSettings,
                            previousTenantSettings,
                            algorithm
                        )
                    );
                    if (!isAdded)
                    {
                        continue;
                    }

                    await mediator.SendAsync(
                        new TenantChangedCommand(tenantSettings.TenantId, SettingsChanged: true),
                        cancellationToken
                    );
                }
                catch (Exception exception) when (exception is not OperationCanceledException)
                {
                    // One tenant that cannot be re-seeded, say one with a settings document the new server settings
                    // reject, must not stop the pass: the others still need their keys.
                    Logger.TenantKeyReseedFailed(tenantSettings.TenantId, exception);
                }
            }

            page = page with { Cursor = result.NextCursor };
        } while (page.Cursor is not null);
    }
}
