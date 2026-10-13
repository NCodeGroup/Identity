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

using System.Buffers;
using System.Buffers.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Extensions.DataProtection;
using NCode.Identity.Logic;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Server.Logging;
using NCode.Mediator;

namespace NCode.Identity.Server.ConformanceEnvironment;

/// <summary>
/// A <strong>conformance-only</strong> seed handler that provisions the confidential clients and the deterministic
/// test user that the OpenID Foundation conformance suite drives, into each workload tenant, so a launched Playground
/// host always exposes the same identities with no manual API calls. It is idempotent, inert unless its inputs are
/// configured, and never targets the control-plane root tenant. It must not be used in production.
/// </summary>
internal sealed class ConformanceSeedHandler(
    ConformanceSeedOptions options,
    IDataProtectorFactory<PersistedSecret> dataProtectorFactory,
    ICryptoService cryptoService,
    TimeProvider timeProvider,
    ILogger<ConformanceSeedHandler> logger
) : ICommandHandler<SeedTenantCommand>, ISupportMediatorPriority
{
    private ConformanceSeedOptions Options { get; } = options;
    private IDataProtectorFactory<PersistedSecret> DataProtectorFactory { get; } =
        dataProtectorFactory;
    private ICryptoService CryptoService { get; } = cryptoService;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<ConformanceSeedHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public int MediatorPriority => DefaultMediatorPriorities.Low;

    /// <inheritdoc />
    public async ValueTask HandleAsync(
        SeedTenantCommand command,
        CancellationToken cancellationToken
    )
    {
        var context = command.Context;
        if (!TargetsTenant(context))
        {
            return;
        }

        foreach (var client in Options.Clients)
        {
            if (client.ShouldSeed)
            {
                await SeedClientAsync(context, client, cancellationToken);
            }
        }

        if (Options.SeedTestUser)
        {
            await SeedTestUserAsync(context, cancellationToken);
        }
    }

    private async ValueTask SeedClientAsync(
        TenantSeedContext context,
        ConformanceClientSeed client,
        CancellationToken cancellationToken
    )
    {
        var clientId = client.ClientId;
        var secret = client.ClientSecret;
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret))
        {
            return;
        }

        var tenantId = context.TenantId;

        var store = context.StoreManager.GetStore<IClientStore>();
        var existing = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        // The secret is supplied Base64Url-encoded (not plaintext) so a raw process-memory snapshot does not expose it
        // directly; decode it straight into a pinned, zeroed-on-return buffer threaded as ReadOnlyMemory.
        var secretMaxLength = Base64Url.GetMaxDecodedLength(secret.Length);
        using var secretOwner = SecureMemoryPool<byte>.Shared.Rent(secretMaxLength);
        var secretWritten = Base64Url.DecodeFromChars(secret, secretOwner.Memory.Span);
        var clientSecretBytes = secretOwner.Memory[..secretWritten];

        var settings = JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.RedirectUris] = client.RedirectUris,
                [OpenIdSettingNames.GrantTypesSupported] = client.GrantTypes,
                [OpenIdSettingNames.ScopesSupported] = client.Scopes,
                [OpenIdSettingNames.TokenEndpointAuthMethodsSupported] =
                    client.TokenEndpointAuthMethods,
                [OpenIdSettingNames.CodeChallengeMethodsSupported] = client.CodeChallengeMethods,
            }
        );

        var persistedClient = new PersistedClient
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
                Value = [CreatePersistedSecret(clientSecretBytes)],
            },
        };

        await store.AddAsync(persistedClient, cancellationToken);

        Logger.ConformanceClientSeeded(clientId, tenantId);
    }

    private async ValueTask SeedTestUserAsync(
        TenantSeedContext context,
        CancellationToken cancellationToken
    )
    {
        var userName = Options.TestUserName;
        var password = Options.TestUserPassword;
        if (string.IsNullOrEmpty(userName) || string.IsNullOrEmpty(password))
        {
            return;
        }

        var tenantId = context.TenantId;

        var store = context.StoreManager.GetStore<ILocalAccountStore>();
        var existing = await store.GetByUserNameOrDefaultAsync(userName, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var account = new PersistedLocalAccount
        {
            TenantId = tenantId,
            LocalAccountId = Guid.NewGuid().ToString("N"),
            UserName = userName,
            Email = Options.TestUserEmail,
            EmailVerified = true,
            IsEnabled = true,
            Claims = [],
            ConcurrencyToken = string.Empty,
        };

        // Encode the password into a pinned, zeroed-on-return buffer so the credential never lingers on the GC heap;
        // the store hashes it internally.
        var byteCount = SecureEncoding.UTF8.GetByteCount(password);
        using var owner = SecureMemoryPool<byte>.Shared.Rent(byteCount);
        var written = SecureEncoding.UTF8.GetBytes(password, owner.Memory.Span);

        await store.AddAsync(account, owner.Memory[..written], cancellationToken);

        Logger.ConformanceTestUserSeeded(userName, tenantId);
    }

    private bool TargetsTenant(TenantSeedContext context)
    {
        // The conformance clients and test user are workload-plane artifacts; never seed them into the control-plane
        // root tenant.
        if (context.Plane != TenantPlane.Workload)
        {
            return false;
        }

        var configuredTenantId = Options.TenantId;
        return string.IsNullOrEmpty(configuredTenantId)
            || string.Equals(context.TenantId, configuredTenantId, StringComparison.Ordinal);
    }

    private PersistedSecret CreatePersistedSecret(ReadOnlyMemory<byte> clientSecret)
    {
        var protector = DataProtectorFactory.CreateDataProtector();

        var writer = new ArrayBufferWriter<byte>();
        protector.ProtectSpan(clientSecret.Span, ref writer);

        var now = TimeProvider.GetUtcNow();
        return new PersistedSecret
        {
            // An opaque, unique id per secret (the repo-wide CSPRNG resource-id scheme) so the id reveals nothing.
            SecretId = CryptoService.GenerateResourceId(),
            Use = null,
            Algorithm = null,
            CreatedWhen = now,
            ExpiresWhen = now.AddYears(100),
            SecretType = SecretTypes.Symmetric,
            KeySizeBits = clientSecret.Length * 8,
            EncodedValue = Base64Url.EncodeToString(writer.WrittenSpan),
        };
    }
}
