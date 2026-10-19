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
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Mediator;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// A <strong>development-only</strong> seed handler that provisions the public (PKCE-only) single-page-application
/// clients and a deterministic administrator user into each workload tenant, so a local host can exercise the
/// interactive authorization-code + PKCE flow and every frontend surface end to end. It is idempotent, inert unless
/// its inputs are configured, and never targets the control-plane root tenant.
/// </summary>
/// <remarks>
/// A seeded client's settings come from configuration, so the document is validated against the registered setting
/// descriptors before it is stored: a value the server cannot honor stops the seeding with an error naming the client,
/// rather than storing a client that fails every request that loads it.
/// </remarks>
// See ADR-0140.
internal sealed class DeveloperInteractiveSeedHandler(
    DeveloperInteractiveSeedOptions options,
    ISettingSerializer settingSerializer,
    IOpenIdEnvironmentProvider openIdEnvironmentProvider
) : ICommandHandler<SeedTenantCommand>, ISupportMediatorPriority
{
    private static readonly string[] PublicClientAuthMethods = ["none"];

    private DeveloperInteractiveSeedOptions Options { get; } = options;
    private ISettingSerializer SettingSerializer { get; } = settingSerializer;
    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; } =
        openIdEnvironmentProvider;

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

        var settings = JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.RedirectUris] = spaClient.RedirectUris,
                [OpenIdSettingNames.TokenEndpointAuthMethodsSupported] = PublicClientAuthMethods,
                [OpenIdSettingNames.RequireCodeChallenge] = true,
                [OpenIdSettingNames.RequireConsent] = spaClient.RequireConsent,
            }
        );
        EnsureSettingsCanBeHonored(clientId, settings);

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

    private void EnsureSettingsCanBeHonored(string clientId, JsonElement settings)
    {
        var jsonOptions = OpenIdEnvironmentProvider.Get().JsonSerializerOptions;
        var settingErrors = SettingSerializer.ValidateSettings(settings, jsonOptions);
        if (settingErrors.Count == 0)
            return;

        var messages = settingErrors.Select(error => error.Message);
        var details = string.Join(" ", messages);
        throw new InvalidOperationException(
            $"The developer client '{clientId}' cannot be seeded: {details}"
        );
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

        // Encode the password into a pinned, zeroed-on-return buffer so the credential never lingers on the GC heap;
        // the store hashes it internally.
        var byteCount = SecureEncoding.UTF8.GetByteCount(password);
        using var owner = SecureMemoryPool<byte>.Shared.Rent(byteCount);
        var written = SecureEncoding.UTF8.GetBytes(password, owner.Memory.Span);

        await store.AddAsync(account, owner.Memory[..written], cancellationToken);
    }

    private bool TargetsTenant(TenantSeedContext context)
    {
        // The SPA client and administrator user are workload-plane artifacts; never seed them into the control-plane
        // root tenant.
        if (context.Plane != TenantPlane.Workload)
        {
            return false;
        }

        var configuredTenantId = Options.TenantId;
        return string.IsNullOrEmpty(configuredTenantId)
            || string.Equals(context.TenantId, configuredTenantId, StringComparison.Ordinal);
    }
}
