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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Tenants.Keys;

/// <summary>
/// Seeds every tenant with a persisted key for each algorithm the provisioning options configure and the tenant holds no
/// usable key for. Keys are generated through the normal secret-persistence path, so the runtime, the JSON Web Key Set,
/// and the management API all read the same keys.
/// </summary>
/// <remarks>
/// The handler only fills gaps. It never removes, replaces, or rotates a key, and a key it cannot read still counts
/// as held: an undecryptable key signals a lost or changed Data Protection key ring, which must fail the tenant rather
/// than be papered over with a new key.
/// </remarks>
// See ADR-0166 (every tenant is seeded through one pipeline) and ADR-0169 (every tenant is provisioned with persisted
// keys).
internal class TenantKeySeedHandler(
    IOptions<OpenIdOptions> optionsAccessor,
    ITenantKeyPolicy tenantKeyPolicy,
    IOpenIdEnvironmentProvider openIdEnvironmentProvider,
    IOpenIdServerProvider openIdServerProvider,
    ISecurityProfileCatalog securityProfileCatalog,
    ISecretGenerator secretGenerator,
    ICryptoService cryptoService,
    TimeProvider timeProvider,
    ILogger<TenantKeySeedHandler> logger
) : ICommandHandler<SeedTenantCommand>, ISupportMediatorPriority
{
    // A provisioned key has no lifetime of its own: nothing enforces a key's expiry, and key rollover (backlog S8) is
    // what will give provisioned keys one.
    private const int KeyLifetimeYears = 100;

    private OpenIdTenantKeyProvisioningOptions Options { get; } =
        optionsAccessor.Value.Tenant.KeyProvisioning;

    private ITenantKeyPolicy TenantKeyPolicy { get; } = tenantKeyPolicy;
    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; } =
        openIdEnvironmentProvider;
    private IOpenIdServerProvider OpenIdServerProvider { get; } = openIdServerProvider;
    private ISecurityProfileCatalog SecurityProfileCatalog { get; } = securityProfileCatalog;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private ICryptoService CryptoService { get; } = cryptoService;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<TenantKeySeedHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        SeedTenantCommand command,
        CancellationToken cancellationToken
    )
    {
        if (!Options.Enabled)
        {
            return;
        }

        var resolution = TenantKeyPolicy.ResolveSpecifications(Options);
        if (resolution.Errors.Count > 0)
        {
            throw new InvalidOperationException(
                "The tenant key provisioning options cannot be honored: "
                    + string.Join(" ", resolution.Errors)
            );
        }

        var context = command.Context;
        var tenant = context.Tenant;
        var held = tenant.Secrets.Value;
        var settings = await GetEffectiveSettingsAsync(tenant, cancellationToken);

        var generated = new List<PersistedSecret>();
        foreach (var specification in resolution.Specifications)
        {
            if (!IsUsed(specification, settings))
            {
                continue;
            }

            if (held.Any(secret => TenantKeyPolicy.IsSatisfiedBy(specification, secret)))
            {
                continue;
            }

            var secret = GenerateSecret(specification);
            await context
                .StoreManager.GetStore<ITenantStore>()
                .AddSecretAsync(tenant.TenantId, secret, cancellationToken);
            generated.Add(secret);

            Logger.TenantKeyProvisioned(tenant.TenantId, specification.Algorithm, secret.SecretId);
        }

        if (generated.Count == 0)
        {
            return;
        }

        // The materialization that follows the seed reads the tenant's secrets from this instance, before the unit of
        // work commits, so it must already carry the keys generated here.
        tenant.Secrets = new PersistedTenantSecrets
        {
            TenantId = tenant.Secrets.TenantId,
            ConcurrencyToken = tenant.Secrets.ConcurrencyToken,
            Value = [.. held, .. generated],
        };
    }

    // The tenant's settings materialize only after the seed, so the seed merges the tenant's persisted document onto
    // the server's settings itself, as the materialized tenant will.
    private async ValueTask<IReadOnlySettingCollection> GetEffectiveSettingsAsync(
        PersistedTenant tenant,
        CancellationToken cancellationToken
    )
    {
        var openIdEnvironment = OpenIdEnvironmentProvider.Get();
        var openIdServer = await OpenIdServerProvider.GetAsync(
            openIdEnvironment,
            cancellationToken
        );
        var tenantSettings = SecurityProfileCatalog.Expand(
            tenant.Settings.Value,
            OpenIdSettingScopes.Tenant,
            openIdEnvironment.JsonSerializerOptions
        );
        return openIdServer.SettingsProvider.Collection.Merge(tenantSettings);
    }

    // A key is provisioned only for an algorithm the tenant can sign or decrypt with; a key for any other algorithm would
    // be published in the tenant's key set and never used.
    private bool IsUsed(
        TenantKeySpecification specification,
        IReadOnlySettingCollection settings
    ) =>
        Options.AlgorithmSettings.Any(settingName =>
            settings.TryGetValue(settingName, out var value)
            && value is IEnumerable<string> algorithms
            && algorithms.Contains(specification.Algorithm, StringComparer.Ordinal)
        );

    private PersistedSecret GenerateSecret(TenantKeySpecification specification)
    {
        var now = TimeProvider.GetUtcNow();

        return SecretGenerator
            .GenerateSecret(
                new GenerateSecretRequest
                {
                    SecretId = CryptoService.GenerateResourceId(),
                    SecretType = specification.SecretType,
                    KeySizeBits = specification.KeySizeBits,
                    Use = specification.Use,
                    Algorithm = specification.Algorithm,
                    CreatedWhen = now,
                    ExpiresWhen = now.AddYears(KeyLifetimeYears),
                }
            )
            .Secret;
    }
}
