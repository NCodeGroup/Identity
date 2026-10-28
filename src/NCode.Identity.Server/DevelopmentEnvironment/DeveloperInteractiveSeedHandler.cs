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

using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Mediator;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// A <strong>development-only</strong> seed handler that provisions the configured public (PKCE-only)
/// single-page-application clients and a deterministic administrator user into the one tenant its options name, so a
/// local host can exercise the interactive authorization-code + PKCE flow and every frontend surface end to end. It is
/// idempotent and inert unless its inputs are configured.
/// </summary>
/// <remarks>
/// A seeded client's settings come from configuration, so the document is checked before it is stored: its settings
/// are validated against the registered setting domains and against the client coherence rules of the tenant it is
/// seeded into, and a failure stops the seeding with an error naming the client, rather than storing a client that
/// fails every request that loads it. The administrator's password must meet the tenant's password policy, and a
/// refused one stops the seeding with an error naming the administrator.
/// </remarks>
// See ADR-0140 and ADR-0206.
internal sealed class DeveloperInteractiveSeedHandler(
    DeveloperInteractiveSeedOptions options,
    SeededClientValidator seededClientValidator,
    SeededLocalAccountValidator seededLocalAccountValidator
) : ICommandHandler<SeedTenantCommand>, ISupportMediatorPriority
{
    private DeveloperInteractiveSeedOptions Options { get; } = options;
    private SeededClientValidator SeededClientValidator { get; } = seededClientValidator;
    private SeededLocalAccountValidator SeededLocalAccountValidator { get; } =
        seededLocalAccountValidator;

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

        foreach (var spaClient in Options.SpaClients)
        {
            if (spaClient.ShouldSeed)
            {
                await SeedSpaClientAsync(context, spaClient, cancellationToken);
            }
        }

        if (Options.SeedAdminUser)
        {
            await SeedAdminUserAsync(context, cancellationToken);
        }
    }

    private async ValueTask SeedSpaClientAsync(
        TenantSeedContext context,
        DeveloperSpaClientSeed spaClient,
        CancellationToken cancellationToken
    )
    {
        var clientId = spaClient.ClientId;
        if (string.IsNullOrEmpty(clientId))
        {
            return;
        }

        var store = context.StoreManager.GetStore<IClientStore>();
        var existing = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        // A single-page application cannot keep a secret, so it registers as a public client (RFC 7591 §2 'none').
        var settings = JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.RedirectUris] = spaClient.RedirectUris,
                [OpenIdSettingNames.TokenEndpointAuthMethod] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .None,
                [OpenIdSettingNames.RequireConsent] = spaClient.RequireConsent,
            }
        );
        await SeededClientValidator.EnsureCanBeSeededAsync(
            "developer client",
            clientId,
            context.Tenant,
            settings,
            cancellationToken
        );

        var persistedClient = new PersistedClient
        {
            TenantId = context.TenantId,
            ClientId = clientId,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = context.TenantId,
                ClientId = clientId,
                Value = settings,
            },
            // A public client authenticates with PKCE and holds no secret.
            Secrets = new PersistedClientSecrets
            {
                TenantId = context.TenantId,
                ClientId = clientId,
                Value = [],
            },
        };

        await store.AddAsync(persistedClient, cancellationToken);
    }

    private async ValueTask SeedAdminUserAsync(
        TenantSeedContext context,
        CancellationToken cancellationToken
    )
    {
        var username = Options.AdminUserName;
        var password = Options.AdminPassword;
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return;
        }

        var store = context.StoreManager.GetStore<ILocalAccountStore>();
        var existing = await store.GetByUserNameOrDefaultAsync(username, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        var account = new PersistedLocalAccount
        {
            TenantId = context.TenantId,
            LocalAccountId = Guid.NewGuid().ToString("N"),
            UserName = username,
            Email = Options.AdminEmail,
            EmailVerified = true,
            IsEnabled = true,
            Claims =
            [
                new PersistedLocalAccountClaim
                {
                    Type = JoseClaimNames.Payload.Role,
                    Value = BuiltInRoles.GlobalAdmin,
                },
            ],
            ConcurrencyToken = string.Empty,
        };

        await SeededLocalAccountValidator.EnsureCanBeSeededAsync(
            "developer administrator",
            context.Tenant,
            account,
            password,
            cancellationToken
        );

        // Encode the password into a pinned, zeroed-on-return buffer so the credential never lingers on the GC heap;
        // the store hashes it internally.
        var byteCount = SecureEncoding.UTF8.GetByteCount(password);
        using var owner = SecureMemoryPool<byte>.Shared.Rent(byteCount);
        var written = SecureEncoding.UTF8.GetBytes(password, owner.Memory.Span);

        await store.AddAsync(account, owner.Memory[..written], cancellationToken);
    }

    private bool TargetsTenant(TenantSeedContext context) =>
        // The registration requires the tenant whenever there is something to seed (ADR-0166): a tenant created at
        // runtime must not inherit a well-known administrator.
        string.Equals(context.TenantId, Options.TenantId, StringComparison.Ordinal);
}
