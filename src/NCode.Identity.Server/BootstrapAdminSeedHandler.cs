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

using System.Buffers.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Buffers;
using NCode.Identity.Cryptography;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Server.Logging;
using NCode.Mediator;

namespace NCode.Identity.Server;

/// <summary>
/// Seeds the configured bootstrap administrator client into the workload tenant as part of the tenant seed fan-out.
/// The client's management-audience tokens are stamped with the <c>GlobalAdmin</c> role by
/// <see cref="BootstrapAdminAccessTokenClaimsHandler"/>, so an operator can make the first authorized management call.
/// The handler is idempotent and inert unless a bootstrap client id and secret are configured; it never targets the
/// control-plane root tenant.
/// </summary>
internal sealed class BootstrapAdminSeedHandler(
    IOptions<BootstrapAdminOptions> bootstrapOptionsAccessor,
    ISecretGenerator secretGenerator,
    ICryptoService cryptoService,
    TimeProvider timeProvider,
    ILogger<BootstrapAdminSeedHandler> logger
) : ICommandHandler<SeedTenantCommand>, ISupportMediatorPriority
{
    private BootstrapAdminOptions Options { get; } = bootstrapOptionsAccessor.Value;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private ICryptoService CryptoService { get; } = cryptoService;
    private TimeProvider TimeProvider { get; } = timeProvider;
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
            // The configured secret is authoritative. Re-protect it with the current data-protection keys so a stale
            // or undecryptable stored secret (for example after a key-ring change) is healed instead of leaving the
            // bootstrap client permanently unable to authenticate.
            await RefreshClientSecretAsync(store, clientId, clientSecretBytes, cancellationToken);
            Logger.BootstrapAdminClientSecretRefreshed(clientId, tenantId);
            return;
        }

        var persistedClient = CreateClient(tenantId, clientId, clientSecretBytes);

        await store.AddAsync(persistedClient, cancellationToken);

        Logger.BootstrapAdminClientSeeded(clientId, tenantId);
    }

    private async ValueTask RefreshClientSecretAsync(
        IClientStore store,
        string clientId,
        ReadOnlyMemory<byte> clientSecret,
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
            CreatePersistedSecret(clientSecret),
            cancellationToken
        );
    }

    private bool TargetsTenant(TenantSeedContext context)
    {
        // The bootstrap administrator is a workload-plane client; it is never seeded into the control-plane root tenant.
        if (context.Plane != TenantPlane.Workload)
        {
            return false;
        }

        var configuredTenantId = Options.TenantId;
        return string.IsNullOrEmpty(configuredTenantId)
            || string.Equals(context.TenantId, configuredTenantId, StringComparison.Ordinal);
    }

    private PersistedClient CreateClient(
        string tenantId,
        string clientId,
        ReadOnlyMemory<byte> clientSecret
    )
    {
        var persistedSecret = CreatePersistedSecret(clientSecret);

        var emptySettings = JsonSerializer.SerializeToElement(new Dictionary<string, object>());

        return new PersistedClient
        {
            TenantId = tenantId,
            ClientId = clientId,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = tenantId,
                ClientId = clientId,
                Value = emptySettings,
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = tenantId,
                ClientId = clientId,
                Value = [persistedSecret],
            },
        };
    }

    private PersistedSecret CreatePersistedSecret(ReadOnlyMemory<byte> clientSecret)
    {
        var now = TimeProvider.GetUtcNow();

        // Persist the configured client_secret through the single shared-secret primitive (ADR-0110) so its stored
        // UTF-8 shape cannot drift from generated secrets.
        return SecretGenerator.ImportSharedSecret(
            new ImportSharedSecretRequest
            {
                // An opaque, unique id per secret (the repo-wide CSPRNG resource-id scheme) so a refresh (remove-then-add
                // in one unit of work) never collides with the outgoing secret's id, and the id reveals nothing.
                SecretId = CryptoService.GenerateResourceId(),
                Use = null,
                Algorithm = null,
                CreatedWhen = now,
                ExpiresWhen = now.AddYears(100),
            },
            clientSecret.Span
        );
    }
}
