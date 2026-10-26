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
using NCode.Buffers;
using NCode.Identity.Cryptography;
using NCode.Identity.Jose;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
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

namespace NCode.Identity.Server.ConformanceEnvironment;

/// <summary>
/// A <strong>conformance-only</strong> seed handler that provisions the confidential clients — authenticated by a
/// secret or by the public keys they register — and the deterministic
/// test user that the OpenID Foundation conformance suite drives, into the one tenant its options name, so a launched
/// OpenID host always exposes the same identities with no manual API calls. It is idempotent and inert unless its
/// inputs are configured. It must not be used in production.
/// </summary>
/// <remarks>
/// A seeded client's settings come from configuration, so the document is validated against the registered setting
/// descriptors before it is stored: a value the server cannot honor stops the seeding with an error naming the client,
/// rather than storing a client that fails every request that loads it. Its key set is imported under the client-key
/// policy a dynamically registered <c>jwks</c> follows, and a key set the policy refuses stops the seeding the same way.
/// So does a setting that a client coherence rule refuses. The test user's password must meet the tenant's password
/// policy, and a refused one stops the seeding with an error naming the test user.
/// </remarks>
// See ADR-0140.
internal sealed class ConformanceSeedHandler(
    ConformanceSeedOptions options,
    ISecretGenerator secretGenerator,
    ISecretProtectorProvider secretProtectorProvider,
    IClientKeyImporter clientKeyImporter,
    ICryptoService cryptoService,
    TimeProvider timeProvider,
    SeededClientValidator seededClientValidator,
    SeededLocalAccountValidator seededLocalAccountValidator,
    ILogger<ConformanceSeedHandler> logger
) : ICommandHandler<SeedTenantCommand>, ISupportMediatorPriority
{
    // The Basic OP set a client uses when its seed configures no grant types.
    private static readonly string[] DefaultGrantTypes =
    [
        OpenIdConstants.GrantTypes.AuthorizationCode,
        OpenIdConstants.GrantTypes.RefreshToken,
    ];
    private ConformanceSeedOptions Options { get; } = options;
    private ISecretGenerator SecretGenerator { get; } = secretGenerator;
    private ISecretProtectorProvider SecretProtectorProvider { get; } = secretProtectorProvider;
    private IClientKeyImporter ClientKeyImporter { get; } = clientKeyImporter;
    private ICryptoService CryptoService { get; } = cryptoService;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private SeededClientValidator SeededClientValidator { get; } = seededClientValidator;
    private SeededLocalAccountValidator SeededLocalAccountValidator { get; } =
        seededLocalAccountValidator;
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

        foreach (var (clientId, client) in Options.Clients)
        {
            if (!string.IsNullOrEmpty(clientId) && client.ShouldSeed)
            {
                await SeedClientAsync(context, clientId, client, cancellationToken);
            }
        }

        if (Options.SeedTestUser)
        {
            await SeedTestUserAsync(context, cancellationToken);
        }
    }

    private async ValueTask SeedClientAsync(
        TenantSeedContext context,
        string clientId,
        ConformanceClientSeed client,
        CancellationToken cancellationToken
    )
    {
        var tenantId = context.TenantId;

        var store = context.StoreManager.GetStore<IClientStore>();
        var existing = await store.GetOrDefaultAsync(clientId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        // The registered method (RFC 7591 §2) decides the client type, and it is the only method the client may use.
        var settingValues = new Dictionary<string, object>
        {
            [OpenIdSettingNames.RedirectUris] = client.RedirectUris,
            [OpenIdSettingNames.GrantTypesSupported] =
                client.GrantTypes.Length > 0 ? client.GrantTypes : DefaultGrantTypes,
            [OpenIdSettingNames.TokenEndpointAuthMethod] = client.TokenEndpointAuthMethod,
        };
        if (client.CodeChallengeMethods.Length > 0)
            settingValues[OpenIdSettingNames.CodeChallengeMethodsSupported] =
                client.CodeChallengeMethods;
        if (client.SecurityProfiles.Length > 0)
            settingValues[OpenIdSettingNames.SecurityProfiles] = client.SecurityProfiles;

        var settings = JsonSerializer.SerializeToElement(settingValues);
        await SeededClientValidator.EnsureCanBeSeededAsync(
            "conformance client",
            clientId,
            context.Tenant,
            settings,
            cancellationToken
        );

        var secrets = new List<PersistedSecret>();
        if (!string.IsNullOrEmpty(client.ClientSecret))
        {
            // A client seeded with only its keys never loads the tenant's master key.
            var protector = await SecretProtectorProvider.GetProtectorAsync(
                SecretOwner.ForTenant(tenantId),
                [],
                cancellationToken
            );
            secrets.Add(CreatePersistedSecret(protector, client.ClientSecret));
        }
        if (!string.IsNullOrEmpty(client.Jwks))
            secrets.AddRange(ImportKeys(clientId, client.Jwks));

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
                Value = secrets,
            },
        };

        await store.AddAsync(persistedClient, cancellationToken);

        Logger.ConformanceClientSeeded(clientId, tenantId);
    }

    private IReadOnlyList<PersistedSecret> ImportKeys(string clientId, string jwks)
    {
        using var document = ParseKeySet(clientId, jwks);
        if (
            ClientKeyImporter.TryImportRegisteredKeySet(
                document.RootElement,
                TimeProvider.GetUtcNow(),
                out var keys,
                out var error
            )
        )
        {
            return keys;
        }

        throw new InvalidOperationException(
            $"The conformance client '{clientId}' cannot be seeded: its key set is invalid. {error}"
        );
    }

    private static JsonDocument ParseKeySet(string clientId, string jwks)
    {
        try
        {
            return JsonDocument.Parse(jwks);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The conformance client '{clientId}' cannot be seeded: its key set is not JSON.",
                exception
            );
        }
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
            Claims = BuildTestUserProfileClaims(),
            ConcurrencyToken = string.Empty,
        };

        await SeededLocalAccountValidator.EnsureCanBeSeededAsync(
            "conformance test user",
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

        Logger.ConformanceTestUserSeeded(userName, tenantId);
    }

    private List<PersistedLocalAccountClaim> BuildTestUserProfileClaims()
    {
        var givenName = Options.TestUserGivenName;
        var familyName = Options.TestUserFamilyName;

        var claims = new List<PersistedLocalAccountClaim>();

        void Add(string type, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                claims.Add(new PersistedLocalAccountClaim { Type = type, Value = value });
            }
        }

        Add(JoseClaimNames.Payload.GivenName, givenName);
        Add(JoseClaimNames.Payload.FamilyName, familyName);

        // Compose the displayed 'name' from whichever name parts are configured so the suite's EnsureUserInfoContainsName
        // check (a 'profile' scope request) always finds one.
        var displayName = string.Join(
            ' ',
            new[] { givenName, familyName }.Where(part => !string.IsNullOrEmpty(part))
        );
        Add(JoseClaimNames.Payload.Name, displayName);

        // Seed the full standard 'profile', 'address', and 'phone' scope claim sets with deterministic values so the
        // suite's VerifyScopesReturnedInUserInfoClaims check finds every scope-mapped claim in the UserInfo response
        // (OIDC Core §5.4); the UserInfo enricher coerces the typed claims (updated_at → number, address → object,
        // phone_number_verified → boolean) on the way out.
        Add(JoseClaimNames.Payload.MiddleName, "Q");
        Add(JoseClaimNames.Payload.Nickname, "Con");
        Add(JoseClaimNames.Payload.PreferredUsername, Options.TestUserName);
        Add(JoseClaimNames.Payload.Profile, "https://op.localhost/users/conformance-user");
        Add(
            JoseClaimNames.Payload.Picture,
            "https://op.localhost/users/conformance-user/picture.png"
        );
        Add(JoseClaimNames.Payload.Website, "https://op.localhost");
        Add(JoseClaimNames.Payload.Gender, "unspecified");
        Add(JoseClaimNames.Payload.Birthdate, "2000-01-01");
        Add(JoseClaimNames.Payload.Zoneinfo, "America/Los_Angeles");
        Add(JoseClaimNames.Payload.Locale, "en-US");
        Add(JoseClaimNames.Payload.UpdatedAt, "1700000000");

        // The address claim is a structured JSON object (OIDC Core §5.1.1); it is persisted as its JSON text and the
        // enricher parses it back into an object for the response.
        Add(
            JoseClaimNames.Payload.Address,
            """
            {"formatted":"123 Main St\nSpringfield, CA 90000\nUS","street_address":"123 Main St","locality":"Springfield","region":"CA","postal_code":"90000","country":"US"}
            """
        );

        Add(JoseClaimNames.Payload.PhoneNumber, "+15555550100");
        Add(JoseClaimNames.Payload.PhoneNumberVerified, "true");

        return claims;
    }

    private bool TargetsTenant(TenantSeedContext context) =>
        // The registration requires the tenant whenever there is something to seed (ADR-0166): a tenant created at
        // runtime must not inherit the conformance clients and test user.
        string.Equals(context.TenantId, Options.TenantId, StringComparison.Ordinal);

    private PersistedSecret CreatePersistedSecret(ISecretProtector protector, string encodedSecret)
    {
        // The secret is supplied Base64Url-encoded (not plaintext) so a raw process-memory snapshot does not expose it
        // directly; decode it straight into a pinned, zeroed-on-return buffer.
        var secretMaxLength = Base64Url.GetMaxDecodedLength(encodedSecret.Length);
        using var secretOwner = SecureMemoryPool<byte>.Shared.Rent(secretMaxLength);
        var secretWritten = Base64Url.DecodeFromChars(encodedSecret, secretOwner.Memory.Span);
        var clientSecret = secretOwner.Memory.Span[..secretWritten];

        var now = TimeProvider.GetUtcNow();

        // Persist the given client_secret through the single shared-secret primitive (ADR-0110) so its stored UTF-8
        // shape cannot drift from generated secrets.
        return SecretGenerator.ImportSharedSecret(
            protector,
            new ImportSharedSecretRequest
            {
                // An opaque, unique id per secret (the repo-wide CSPRNG resource-id scheme) so the id reveals nothing.
                SecretId = CryptoService.GenerateResourceId(),
                Use = null,
                Algorithm = null,
                CreatedWhen = now,
                ExpiresWhen = now.AddYears(100),
            },
            clientSecret
        );
    }
}
