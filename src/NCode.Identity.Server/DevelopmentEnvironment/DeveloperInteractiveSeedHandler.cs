// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.Extensions.Logging;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Management;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Server.Logging;
using NCode.Mediator;

namespace NCode.Identity.Server.DevelopmentEnvironment;

/// <summary>
/// A <strong>development-only</strong> seed handler that provisions the configured public (PKCE-only)
/// single-page-application clients, the configured confidential BFF clients and a deterministic administrator user
/// into the one tenant its options name, so a local host can exercise the interactive authorization-code + PKCE flow
/// and every frontend surface end to end. It is idempotent and inert unless its inputs are configured.
/// </summary>
/// <remarks>
/// A seeded client's settings come from configuration, so the document is checked before it is stored: its settings
/// are validated against the registered setting domains and against the client coherence rules of the tenant it is
/// seeded into, and a failure stops the seeding with an error naming the client, rather than storing a client that
/// fails every request that loads it. A BFF client's key set is imported under the client-key policy a dynamically
/// registered <c>jwks</c> follows, and a key set the policy refuses stops the seeding the same way. A BFF client is
/// owned by its configuration, which the AppHost writes from the key files it owns: a stored BFF client whose settings
/// and keys match its configuration is left as it is, and one that differs is removed, with its grants, codes and
/// tokens, and stored again from the configuration. A BFF client entry that names a client id but lacks its key set or
/// its redirect URIs is skipped with a warning that names the missing configuration keys. The administrator's password
/// must meet the tenant's password policy, and a refused one stops the seeding with an error naming the administrator.
/// </remarks>
// See ADR-1140 and ADR-1206.
internal sealed class DeveloperInteractiveSeedHandler(
    DeveloperInteractiveSeedOptions options,
    IClientKeyImporter clientKeyImporter,
    TimeProvider timeProvider,
    SeededClientValidator seededClientValidator,
    SeededLocalAccountValidator seededLocalAccountValidator,
    ILogger<DeveloperInteractiveSeedHandler> logger
) : ICommandHandler<SeedTenantCommand>, ISupportMediatorPriority
{
    // The grant types of a BFF client: the code flow and the refresh of its session's tokens.
    private static string[] BffGrantTypes { get; } =
    [OpenIdProtocol.GrantTypes.AuthorizationCode, OpenIdProtocol.GrantTypes.RefreshToken];

    private DeveloperInteractiveSeedOptions Options { get; } = options;
    private IClientKeyImporter ClientKeyImporter { get; } = clientKeyImporter;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private SeededClientValidator SeededClientValidator { get; } = seededClientValidator;
    private SeededLocalAccountValidator SeededLocalAccountValidator { get; } =
        seededLocalAccountValidator;
    private ILogger<DeveloperInteractiveSeedHandler> Logger { get; } = logger;

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

        for (var index = 0; index < Options.BffClients.Count; ++index)
        {
            var bffClient = Options.BffClients[index];
            if (bffClient.ShouldSeed)
            {
                await SeedBffClientAsync(context, bffClient, cancellationToken);
            }
            else if (!string.IsNullOrEmpty(bffClient.ClientId))
            {
                WarnAboutSkippedBffClient(bffClient.ClientId, bffClient, index);
            }
        }

        if (Options.SeedAdminUser)
        {
            await SeedAdminUserAsync(context, cancellationToken);
        }
    }

    private async ValueTask SeedBffClientAsync(
        TenantSeedContext context,
        DeveloperBffClientSeed bffClient,
        CancellationToken cancellationToken
    )
    {
        var clientId = bffClient.ClientId;
        var jwks = bffClient.Jwks;
        if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(jwks))
        {
            return;
        }

        // A BFF client is confidential: it authenticates with a signed assertion, pushes its authorization requests
        // and holds sender-constrained tokens.
        var settingValues = new Dictionary<string, object>
        {
            [OpenIdSettingNames.RedirectUris] = bffClient.RedirectUris,
            [OpenIdSettingNames.TokenEndpointAuthMethod] = OpenIdProtocol
                .ClientAuthenticationMethods
                .PrivateKeyJwt,
            [OpenIdSettingNames.GrantTypesSupported] = BffGrantTypes,
            [OpenIdSettingNames.RequirePushedAuthorizationRequests] = true,
            [OpenIdSettingNames.RequireDPoPBoundAccessTokens] = true,
        };
        if (bffClient.PostLogoutRedirectUris.Length > 0)
        {
            settingValues[OpenIdSettingNames.PostLogoutRedirectUris] =
                bffClient.PostLogoutRedirectUris;
        }

        if (!string.IsNullOrEmpty(bffClient.BackchannelLogoutUri))
        {
            settingValues[OpenIdSettingNames.BackChannelLogoutUri] = bffClient.BackchannelLogoutUri;
            settingValues[OpenIdSettingNames.BackChannelLogoutSessionRequired] = true;
        }

        var settings = JsonSerializer.SerializeToElement(settingValues);
        await SeededClientValidator.EnsureCanBeSeededAsync(
            "developer BFF client",
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
            Secrets = new PersistedClientSecrets
            {
                TenantId = context.TenantId,
                ClientId = clientId,
                Value = ImportKeys(clientId, jwks),
            },
        };

        var store = context.StoreManager.GetStore<IClientStore>();
        var existing = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (existing is not null)
        {
            if (MatchesConfiguration(existing, persistedClient))
            {
                return;
            }

            // The removal also removes the client's grants, codes and tokens, none of which a changed key or
            // redirect URI can keep using.
            await store.RemoveAsync(clientId, cancellationToken);
            Logger.DeveloperBffClientReplaced(clientId);
        }

        await store.AddAsync(persistedClient, cancellationToken);
    }

    private void WarnAboutSkippedBffClient(
        string clientId,
        DeveloperBffClientSeed bffClient,
        int index
    )
    {
        var prefix = $"{DeveloperInteractiveSeedOptions.DefaultSectionName}:BffClients:{index}";
        var missingKeys = new List<string>(capacity: 2);
        if (string.IsNullOrEmpty(bffClient.Jwks))
        {
            missingKeys.Add($"{prefix}:Jwks");
        }

        if (bffClient.RedirectUris.Length == 0)
        {
            missingKeys.Add($"{prefix}:RedirectUris");
        }

        Logger.DeveloperBffClientSkipped(clientId, string.Join(" and ", missingKeys));
    }

    private static bool MatchesConfiguration(PersistedClient stored, PersistedClient configured) =>
        JsonElement.DeepEquals(stored.Settings.Value, configured.Settings.Value)
        && KeysMatch(stored.Secrets.Value, configured.Secrets.Value);

    // The keys are compared as an unordered set by what makes a key usable. The secret identifier, which the
    // importer generates per import, and the timestamps are not part of the comparison.
    private static bool KeysMatch(
        IReadOnlyCollection<PersistedSecret> stored,
        IReadOnlyCollection<PersistedSecret> configured
    )
    {
        if (stored.Count != configured.Count)
        {
            return false;
        }

        return stored.Select(KeyIdentity).ToHashSet().SetEquals(configured.Select(KeyIdentity));
    }

    private static (
        string? KeyId,
        string? Use,
        string? Algorithm,
        string SecretType,
        string SecretEncoding,
        string EncodedValue
    ) KeyIdentity(PersistedSecret key) =>
        (key.KeyId, key.Use, key.Algorithm, key.SecretType, key.SecretEncoding, key.EncodedValue);

    private IReadOnlyList<PersistedSecret> ImportKeys(string clientId, string jwks)
    {
        var jsonElement = ParseKeySet(clientId, jwks);
        if (
            ClientKeyImporter.TryImportRegisteredKeySet(
                jsonElement,
                TimeProvider.GetUtcNow(),
                out var keys,
                out var error
            )
        )
        {
            return keys;
        }

        throw new InvalidOperationException(
            $"The developer BFF client '{clientId}' cannot be seeded: its key set is invalid. {error}"
        );
    }

    private static JsonElement ParseKeySet(string clientId, string jwks)
    {
        try
        {
            return JsonElement.Parse(jwks);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The developer BFF client '{clientId}' cannot be seeded: its key set is not JSON.",
                exception
            );
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
        using var owner = BufferFactory.Rent(byteCount, isSensitive: true, out Memory<byte> buffer);
        var written = SecureEncoding.UTF8.GetBytes(password, buffer.Span);

        await store.AddAsync(account, buffer[..written], cancellationToken);
    }

    private bool TargetsTenant(TenantSeedContext context) =>
        // The registration requires the tenant whenever there is something to seed (ADR-1166): a tenant created at
        // runtime must not inherit a well-known administrator.
        string.Equals(context.TenantId, Options.TenantId, StringComparison.Ordinal);
}
