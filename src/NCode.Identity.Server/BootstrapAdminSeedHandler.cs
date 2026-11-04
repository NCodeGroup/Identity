// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.Logging;
using NCode.Mediator;

namespace NCode.Identity.Server;

/// <summary>
/// Seeds the configured bootstrap administrator client into the configured bootstrap tenant as part of the tenant seed
/// fan-out. The client registers <c>client_secret_basic</c>, so it presents the configured secret with HTTP Basic
/// authentication. The client's management-audience tokens are stamped with the <c>GlobalAdmin</c> role by
/// <see cref="BootstrapAdminAccessTokenClaimsHandler"/>, so an operator can make the first authorized management call.
/// The handler is idempotent and inert unless a bootstrap client id and secret are configured, and it seeds exactly the
/// one tenant <see cref="BootstrapAdminOptions.TenantId"/> names — never every tenant, which would hand a GlobalAdmin
/// credential to each tenant created at runtime. The client is checked before it is stored, so a ceiling that excludes
/// <c>client_secret_basic</c> stops the seed. An existing client is left as it is; the reconciliation of the server's
/// settings reports it, as it reports every stored client, when the settings make it incoherent.
/// </summary>
// See ADR-1044 (the bootstrap administrator), ADR-1166 (a seeder that names a tenant seeds exactly that tenant), and
// ADR-1219 (an existing client is reported by the reconciliation).
internal sealed class BootstrapAdminSeedHandler(
    IOptions<BootstrapAdminOptions> bootstrapOptionsAccessor,
    IClientSecretIssuer clientSecretIssuer,
    ISecretProtectorProvider secretProtectorProvider,
    ICryptoService cryptoService,
    TimeProvider timeProvider,
    SeededClientValidator seededClientValidator,
    ILogger<BootstrapAdminSeedHandler> logger
) : ICommandHandler<SeedTenantCommand>, ISupportMediatorPriority
{
    // The bootstrap credential is a shared secret, which RFC 6749 §2.3.1 has the client present with HTTP Basic.
    private const string SeededAuthenticationMethod = OpenIdProtocol
        .ClientAuthenticationMethods
        .ClientSecretBasic;

    private BootstrapAdminOptions Options { get; } = bootstrapOptionsAccessor.Value;
    private IClientSecretIssuer ClientSecretIssuer { get; } = clientSecretIssuer;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private ICryptoService CryptoService { get; } = cryptoService;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private SeededClientValidator SeededClientValidator { get; } = seededClientValidator;
    private ILogger<BootstrapAdminSeedHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        SeedTenantCommand command,
        CancellationToken cancellationToken
    )
    {
        var clientId = Options.ClientId;
        var clientSecret = Options.ClientSecret;
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
        {
            return;
        }

        var context = command.Context;
        if (!TargetsTenant(context))
        {
            return;
        }

        var tenantId = context.TenantId;
        var store = context.StoreManager.GetStore<IClientStore>();

        // The configured secret is supplied Base64Url-encoded (not plaintext) so a raw process-memory snapshot does
        // not expose it directly; decode it straight into a pinned, zeroed-on-return buffer threaded as ReadOnlyMemory.
        var secretMaxLength = Base64Url.GetMaxDecodedLength(clientSecret.Length);
        using var secretOwner = SecureMemoryPool<byte>.Shared.Rent(secretMaxLength);
        var secretWritten = Base64Url.DecodeFromChars(clientSecret, secretOwner.Memory.Span);
        var clientSecretBytes = secretOwner.Memory[..secretWritten];

        var existing = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (existing is not null)
        {
            // The configured secret is authoritative. It is stored again under the tenant's current master key, so a
            // stale or undecryptable stored secret is healed instead of leaving the bootstrap client permanently unable
            // to authenticate. Its form follows the method the client is registered for, which an operator may set to
            // another method than the seeded one (ADR-1391).
            var tokenEndpointAuthMethod = await ClientSecretIssuer.GetTokenEndpointAuthMethodAsync(
                context.Tenant.TeamId,
                tenantId,
                context.Tenant.Settings.Value,
                existing.ClientId,
                existing.Settings.Value,
                cancellationToken
            );
            var refreshProtector = await GetProtectorAsync(tenantId, cancellationToken);
            await RefreshClientSecretAsync(
                store,
                refreshProtector,
                clientId,
                clientSecretBytes,
                tokenEndpointAuthMethod,
                cancellationToken
            );
            Logger.BootstrapAdminClientSecretRefreshed(clientId, tenantId);
            return;
        }

        var settings = CreateSettings();

        // See ADR-1206.
        await SeededClientValidator.EnsureCanBeSeededAsync(
            "bootstrap administrator client",
            clientId,
            context.Tenant,
            settings,
            cancellationToken
        );

        // The tenant's master key is loaded only once the client is accepted, so a refused seed never creates it.
        var protector = await GetProtectorAsync(tenantId, cancellationToken);
        var persistedClient = CreateClient(
            tenantId,
            clientId,
            settings,
            clientSecretBytes,
            protector
        );

        await store.AddAsync(persistedClient, cancellationToken);

        Logger.BootstrapAdminClientSeeded(clientId, tenantId);
    }

    private ValueTask<ISecretProtector> GetProtectorAsync(
        string tenantId,
        CancellationToken cancellationToken
    ) =>
        SecretProtectorProvider.GetProtectorAsync(
            SecretOwner.ForTenant(tenantId),
            [],
            cancellationToken
        );

    private async ValueTask RefreshClientSecretAsync(
        IClientStore store,
        ISecretProtector protector,
        string clientId,
        ReadOnlyMemory<byte> clientSecret,
        string? tokenEndpointAuthMethod,
        CancellationToken cancellationToken
    )
    {
        var existingSecrets = await store.GetSecretsOrDefaultAsync(clientId, cancellationToken);
        if (existingSecrets is not null)
        {
            foreach (var secret in existingSecrets.Value)
            {
                await store.RemoveSecretAsync(clientId, secret.SecretId, cancellationToken);
            }
        }

        await store.AddSecretAsync(
            clientId,
            CreatePersistedSecret(protector, clientSecret, tokenEndpointAuthMethod),
            cancellationToken
        );
    }

    private bool TargetsTenant(TenantSeedContext context) =>
        // BootstrapAdminOptionsValidator requires the tenant whenever the bootstrap administrator is configured. The root
        // (control-plane) tenant is a legitimate home for it, wherever a host serves the root tenant.
        string.Equals(context.TenantId, Options.TenantId, StringComparison.Ordinal);

    private static JsonElement CreateSettings() =>
        JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.TokenEndpointAuthMethod] = SeededAuthenticationMethod,
            }
        );

    private PersistedClient CreateClient(
        string tenantId,
        string clientId,
        JsonElement settings,
        ReadOnlyMemory<byte> clientSecret,
        ISecretProtector protector
    )
    {
        var persistedSecret = CreatePersistedSecret(
            protector,
            clientSecret,
            SeededAuthenticationMethod
        );

        return new PersistedClient
        {
            TenantId = tenantId,
            ClientId = clientId,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = tenantId,
                ClientId = clientId,
                Value = settings,
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = tenantId,
                ClientId = clientId,
                Value = [persistedSecret],
            },
        };
    }

    private PersistedSecret CreatePersistedSecret(
        ISecretProtector protector,
        ReadOnlyMemory<byte> clientSecret,
        string? tokenEndpointAuthMethod
    )
    {
        var now = TimeProvider.GetUtcNow();

        // Persist the configured client_secret through the client-secret issuer (ADR-1110), in the form the client's
        // method needs: a PBKDF2 verifier when the method only compares it, reversible when it keys from it (ADR-1391).
        var request = new SharedSecretRequest
        {
            // An opaque, unique id per secret (the repo-wide CSPRNG resource-id scheme) so a refresh (remove-then-add
            // in one unit of work) never collides with the outgoing secret's id, and the id reveals nothing.
            SecretId = CryptoService.GenerateResourceId(),
            CreatedWhen = now,
            ExpiresWhen = now.AddYears(100),
        };

        return ClientSecretIssuer.ImportClientSecret(
            protector,
            tokenEndpointAuthMethod,
            request,
            clientSecret.Span
        );
    }
}
