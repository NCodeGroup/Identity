// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Buffers;
using NCode.Identity.OpenId.Accounts;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.Host.Database;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.DevelopmentEnvironment;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> that boots the OpenID <c>Host</c> project in-process
/// through the full ASP.NET Core HTTP pipeline (via <see cref="TestServer"/>), providing an
/// <see cref="System.Net.Http.HttpClient"/> for over-the-wire integration tests.
/// </summary>
/// <remarks>
/// Each factory instance uses its own uniquely-named in-memory database so tests are fully isolated
/// (EF Core caches its internal service provider by option configuration, so a shared database name
/// would cause parallel test hosts to collide). The tenant signs with the keys the host provisions when it first seeds
/// the tenant, stored in that database under the factory's own root key. A host is stopped through its own
/// run loop (<see cref="ApplicationRunLoopHost"/>), so each hosted service is stopped once.
/// </remarks>
// See ADR-1169.
public class HostApplicationFactory : WebApplicationFactory<HostApiMarker>
{
    /// <summary>The tenant id used by the host's static single-tenant provider.</summary>
    public const string TenantId = "default";

    /// <summary>The team that holds the tenants a test creates through the management API.</summary>
    public const string TeamId = RootTenantOptions.DefaultTeamId;

    /// <summary>
    /// The issuer pinned on the static tenant: the origin <see cref="Microsoft.AspNetCore.TestHost.TestServer"/> serves
    /// requests under, so the URLs the server publishes are ones a test client can call.
    /// </summary>
    public const string TestServerIssuer = "http://localhost";

    /// <summary>The header the test host reads a forwarded mutual-TLS client certificate (base64 DER) from.</summary>
    public const string MutualTlsCertificateHeaderName = "X-Client-Cert";

    /// <summary>
    /// When set, overrides the primary handler of the one SSRF-guarded outbound client with a stub that serves the JSON
    /// this responder returns to every <c>GET</c>, so a <c>jwks_uri</c> fetch can be exercised without a real network
    /// connection. It cannot be set together with <see cref="CertificateRevocationListResponder"/>, because both are
    /// <c>GET</c> requests on the one client.
    /// </summary>
    public Func<HttpRequestMessage, string>? ClientJwksResponder { get; set; }

    /// <summary>
    /// When set, overrides the primary handler of the one SSRF-guarded outbound client with a stub that serves the DER
    /// bytes this responder returns to every <c>GET</c>, so a CRL fetch can be exercised without a real network
    /// connection. It cannot be set together with <see cref="ClientJwksResponder"/>, because both are <c>GET</c>
    /// requests on the one client.
    /// </summary>
    public Func<HttpRequestMessage, byte[]>? CertificateRevocationListResponder { get; set; }

    /// <summary>
    /// When set, overrides the primary handler of the one SSRF-guarded outbound client with a stub that answers
    /// <c>200 OK</c> to every <c>POST</c> and records it in this queue, so the logout token the OP delivers to a
    /// client's <c>backchannel_logout_uri</c> can be captured without a real network connection. The delivery runs on a
    /// background worker, so a test polls the queue after the request that terminates the session.
    /// </summary>
    internal ConcurrentQueue<RecordedHttpExchange>? BackChannelLogoutExchanges { get; set; }

    /// <summary>
    /// Gets a value indicating whether the developer clients of the Development overlay are seeded. They are seeded
    /// only when a derived factory returns <see langword="true"/>, because they are public clients with <c>http</c>
    /// redirect URIs that a test narrowing the platform's or the tenant's ceilings would make incoherent.
    /// </summary>
    protected virtual bool SeedsDeveloperClients => false;

    private string DatabaseName { get; } = $"it-{Guid.NewGuid():N}";

    private string KeyRingDatabaseName { get; } = $"it-keys-{Guid.NewGuid():N}";

    // Shared across every host built from this factory instance (including WithWebHostBuilder-derived hosts) so data
    // seeded through one host is visible to another; a per-host root would give each its own empty database.
    private InMemoryDatabaseRoot DatabaseRoot { get; } = new();

    // Every host this factory builds, including WithWebHostBuilder derivatives that share its database, must unwrap the
    // same master keys, so they share one root key. The hosts also share the factory's key ring, which that root key
    // protects. An inline key is allowed outside Production; this one is generated per run and never committed.
    private string RootKey { get; } = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Gets or sets the platform's own settings document, persisted before the host starts so the platform materializes
    /// with it. Set it before the factory serves any request or reads <see cref="WebApplicationFactory{TEntryPoint}.Services"/>.
    /// </summary>
    public IReadOnlyDictionary<string, object>? PersistedPlatformSettings { get; set; }

    /// <summary>
    /// Sends a first request to the tenant, which forces the static tenant provider to persist the tenant entity that
    /// the stores require as a foreign key.
    /// </summary>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the tenant is persisted.</returns>
    private async ValueTask EnsureTenantPersistedAsync(CancellationToken cancellationToken)
    {
        using var client = CreateClient();
        using var _ = await client.GetAsync("/oauth2/jwks", cancellationToken);
    }

    /// <summary>
    /// Ensures the tenant exists (persisted lazily on first request) and seeds a public client
    /// (<c>none</c> client authentication, no secrets) registered with the given redirect URIs.
    /// </summary>
    public async ValueTask SeedPublicClientAsync(
        string clientId,
        IReadOnlyList<string> redirectUris,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        var settingsJson = System.Text.Json.JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                ["redirect_uris"] = redirectUris,
                ["token_endpoint_auth_method"] = OpenIdProtocol.ClientAuthenticationMethods.None,
            }
        );

        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var persistedClient = new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = clientId,
            Settings = settingsJson,
        }.Build();

        await store.AddAsync(persistedClient, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a public client registered with the given redirect URIs and the extra
    /// client settings.
    /// </summary>
    public async ValueTask SeedPublicClientWithSettingsAsync(
        string clientId,
        IReadOnlyDictionary<string, object> settings,
        IReadOnlyList<string> redirectUris,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        var allSettings = new Dictionary<string, object>(settings)
        {
            ["redirect_uris"] = redirectUris,
            ["token_endpoint_auth_method"] = OpenIdProtocol.ClientAuthenticationMethods.None,
        };

        using var scope = Services.CreateScope();
        await AddClientAsync(
            scope.ServiceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(allSettings),
            [],
            cancellationToken
        );
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client whose public keys are published at the given
    /// <paramref name="jwksUri"/> (resolved at runtime), with no stored secrets, registered with the given redirect URIs
    /// and the extra client settings, so the client can authenticate via <c>private_key_jwt</c>.
    /// </summary>
    public async ValueTask SeedPrivateKeyJwtClientWithSettingsAsync(
        string clientId,
        string jwksUri,
        IReadOnlyDictionary<string, object> settings,
        IReadOnlyList<string> redirectUris,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        var allSettings = new Dictionary<string, object>(settings)
        {
            ["jwks_uri"] = jwksUri,
            ["redirect_uris"] = redirectUris,
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .PrivateKeyJwt,
        };

        using var scope = Services.CreateScope();
        await AddClientAsync(
            scope.ServiceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(allSettings),
            [],
            cancellationToken
        );
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a passwordless local account together with the one-to-one self-issued
    /// federated identity that a real interactive login provisions. Returns the provisioned principal id, the value a
    /// self-issued access token carries as its public <c>sub</c>.
    /// </summary>
    // See ADR-1035.
    public async ValueTask<string> SeedPasswordlessLocalAccountAsync(
        string localAccountId,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        var principalId = $"pid-{Guid.NewGuid():N}";

        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);

        var accountStore = storeManager.GetStore<ILocalAccountStore>();
        await accountStore.AddAsync(
            new PersistedLocalAccount
            {
                TenantId = TenantId,
                LocalAccountId = localAccountId,
                UserName = localAccountId,
                Email = null,
                EmailVerified = false,
                IsEnabled = true,
                Claims = [],
                ConcurrencyToken = string.Empty,
            },
            password: null,
            cancellationToken
        );

        var principalStore = storeManager.GetStore<IFederatedPrincipalStore>();
        await principalStore.AddAsync(
            new PersistedFederatedPrincipal
            {
                TenantId = TenantId,
                PrincipalId = principalId,
                ConcurrencyToken = string.Empty,
            },
            cancellationToken
        );

        var identityStore = storeManager.GetStore<IFederatedIdentityStore>();
        await identityStore.AddAsync(
            new PersistedFederatedIdentity
            {
                TenantId = TenantId,
                FederatedIdentityId = $"fid-{Guid.NewGuid():N}",
                PrincipalId = principalId,
                Issuer = AccountConstants.SelfIssuer,
                Subject = localAccountId,
                JoinKey = null,
                ConcurrencyToken = string.Empty,
            },
            cancellationToken
        );

        await storeManager.SaveChangesAsync(cancellationToken);
        return principalId;
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>client_secret_post</c> whose
    /// symmetric secret matches <paramref name="clientSecret"/>, so the <c>client_credentials</c> grant can be
    /// exercised. When <paramref name="grantTypes"/> is supplied, the client's effective <c>grant_types_supported</c>
    /// setting is restricted to those values (required for grants a client must explicitly allow, such as
    /// <c>password</c>).
    /// </summary>
    public ValueTask SeedConfidentialClientAsync(
        string clientId,
        string clientSecret,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    ) =>
        SeedSharedSecretClientAsync(
            clientId,
            clientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            grantTypes,
            new Dictionary<string, object>(),
            cancellationToken
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>client_secret_basic</c> whose
    /// symmetric secret matches <paramref name="clientSecret"/>, so it authenticates with HTTP Basic authentication.
    /// When <paramref name="grantTypes"/> is supplied, the client's effective <c>grant_types_supported</c> setting is
    /// restricted to those values.
    /// </summary>
    public ValueTask SeedClientSecretBasicClientAsync(
        string clientId,
        string clientSecret,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    ) =>
        SeedSharedSecretClientAsync(
            clientId,
            clientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            grantTypes,
            new Dictionary<string, object>(),
            cancellationToken
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>client_secret_jwt</c> whose
    /// symmetric secret matches <paramref name="clientSecret"/>, so it authenticates with a <c>client_assertion</c> it
    /// signs with that secret. When <paramref name="grantTypes"/> is supplied, the client's effective
    /// <c>grant_types_supported</c> setting is restricted to those values.
    /// </summary>
    public ValueTask SeedClientSecretJwtClientAsync(
        string clientId,
        string clientSecret,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    ) =>
        SeedSharedSecretClientAsync(
            clientId,
            clientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
            grantTypes,
            new Dictionary<string, object>(),
            cancellationToken
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client that registers no client-authentication method, so it
    /// uses the default (<c>client_secret_basic</c>), with a symmetric secret matching <paramref name="clientSecret"/>.
    /// When <paramref name="grantTypes"/> is supplied, the client's effective <c>grant_types_supported</c> setting is
    /// restricted to those values.
    /// </summary>
    public ValueTask SeedClientWithoutAuthenticationMethodAsync(
        string clientId,
        string clientSecret,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    ) =>
        SeedSharedSecretClientAsync(
            clientId,
            clientSecret,
            null,
            grantTypes,
            new Dictionary<string, object>(),
            cancellationToken
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for the shared-secret
    /// <paramref name="tokenEndpointAuthMethod"/> (<c>client_secret_basic</c> or <c>client_secret_post</c>) whose
    /// symmetric secret matches <paramref name="clientSecret"/>, registered with the given redirect URIs and the extra
    /// client settings, so an authorization-code flow can be exercised end to end.
    /// </summary>
    public ValueTask SeedSharedSecretClientWithSettingsAsync(
        string clientId,
        string clientSecret,
        string tokenEndpointAuthMethod,
        IReadOnlyDictionary<string, object> settings,
        IReadOnlyList<string> redirectUris,
        CancellationToken cancellationToken
    ) =>
        SeedSharedSecretClientAsync(
            clientId,
            clientSecret,
            tokenEndpointAuthMethod,
            [],
            new Dictionary<string, object>(settings) { ["redirect_uris"] = redirectUris },
            cancellationToken
        );

    /// <summary>
    /// Reads the secrets stored for a client, so a test can check the form each is stored in.
    /// </summary>
    public async ValueTask<IReadOnlyCollection<PersistedSecret>> GetClientSecretsAsync(
        string clientId,
        CancellationToken cancellationToken
    )
    {
        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var secrets = await storeManager
            .GetStore<IClientStore>()
            .GetSecretsOrDefaultAsync(clientId, cancellationToken);
        return secrets?.Value ?? [];
    }

    /// <summary>
    /// Moves the expiry of every secret a client holds into the past, keeping each secret's identifier and stored value,
    /// so a test can tell an expired secret from an unexpired one.
    /// </summary>
    /// <param name="clientId">The identifier of the client whose secrets expire.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the secrets are stored again.</returns>
    public async ValueTask ExpireClientSecretsAsync(
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var removed = await RemoveClientSecretsAsync(clientId, cancellationToken);
        var expired = removed.Select(CreateExpiredSecret).ToList();

        // The removal is saved first, so the secrets are added again under the identifiers their envelopes are bound to.
        await AddClientSecretsAsync(clientId, expired, cancellationToken);
    }

    /// <summary>
    /// Removes every secret a client holds from the store and saves the removal.
    /// </summary>
    /// <param name="clientId">The identifier of the client whose secrets are removed.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask{TResult}"/> that yields the secrets that were removed.</returns>
    private async ValueTask<IReadOnlyList<PersistedSecret>> RemoveClientSecretsAsync(
        string clientId,
        CancellationToken cancellationToken
    )
    {
        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();
        var secrets = await store.GetSecretsOrDefaultAsync(clientId, cancellationToken);
        var removed = new List<PersistedSecret>();
        foreach (var secret in secrets?.Value ?? [])
        {
            await store.RemoveSecretAsync(clientId, secret.SecretId, cancellationToken);
            removed.Add(secret);
        }

        await storeManager.SaveChangesAsync(cancellationToken);
        return removed;
    }

    /// <summary>
    /// Copies a secret with its expiry and retirement moved into the past, keeping its identifier and stored value.
    /// </summary>
    /// <param name="secret">The secret to copy.</param>
    /// <returns>A <see cref="PersistedSecret"/> that has already expired.</returns>
    private static PersistedSecret CreateExpiredSecret(PersistedSecret secret) =>
        new PersistedSecretBuilder
        {
            SecretId = secret.SecretId,
            KeyId = secret.KeyId,
            Use = secret.Use,
            Algorithm = secret.Algorithm,
            CreatedWhen = secret.CreatedWhen,
            ExpiresWhen = DateTimeOffset.UtcNow.AddMinutes(-1),
            ActivatesWhen = secret.CreatedWhen,
            RetiresWhen = DateTimeOffset.UtcNow.AddMinutes(-1),
            SecretType = secret.SecretType,
            SecretEncoding = secret.SecretEncoding,
            Origin = secret.Origin,
            KeySizeBits = secret.KeySizeBits,
            EncodedValue = secret.EncodedValue,
            PublicKey = secret.PublicKey,
        }.Build();

    /// <summary>
    /// Adds the given secrets to a client in the store and saves the addition.
    /// </summary>
    /// <param name="clientId">The identifier of the client that receives the secrets.</param>
    /// <param name="secrets">The secrets to add.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the secrets are saved.</returns>
    private async ValueTask AddClientSecretsAsync(
        string clientId,
        IReadOnlyList<PersistedSecret> secrets,
        CancellationToken cancellationToken
    )
    {
        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();
        foreach (var secret in secrets)
        {
            await store.AddSecretAsync(clientId, secret, cancellationToken);
        }

        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stores a known <c>client_secret</c> as production stores a supplied one: through the
    /// <see cref="IClientSecretIssuer"/>, which reads the registered method's descriptor, so a method that uses the secret
    /// as a key, such as <c>client_secret_jwt</c>, keeps it as the UTF-8 bytes the method keys from and any other stores
    /// its one-way verifier.
    /// </summary>
    // See ADR-1391.
    private static async ValueTask<PersistedSecret> CreateSharedSecretAsync(
        IServiceProvider serviceProvider,
        string secretId,
        string clientSecret,
        string? tokenEndpointAuthMethod,
        CancellationToken cancellationToken
    )
    {
        var protector = await serviceProvider
            .GetRequiredService<ISecretProtectorProvider>()
            .GetProtectorAsync(SecretOwner.ForTenant(TenantId), [], cancellationToken);

        var request = new SharedSecretRequest
        {
            SecretId = secretId,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(100),
        };

        // A client whose settings register no method is registered for client_secret_basic, RFC 7591's default.
        return serviceProvider
            .GetRequiredService<IClientSecretIssuer>()
            .ImportClientSecret(
                protector,
                tokenEndpointAuthMethod
                    ?? OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
                request,
                SecureEncoding.UTF8.GetBytes(clientSecret)
            );
    }

    private async ValueTask SeedSharedSecretClientAsync(
        string clientId,
        string clientSecret,
        string? tokenEndpointAuthMethod,
        IReadOnlyList<string> grantTypes,
        IReadOnlyDictionary<string, object> additionalSettings,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        // Persist the given client_secret through the same shared-secret primitives production uses (ADR-1391),
        // protected under the tenant's master key.
        var persistedSecret = await CreateSharedSecretAsync(
            serviceProvider,
            $"{clientId}-secret",
            clientSecret,
            tokenEndpointAuthMethod,
            cancellationToken
        );

        var settings = new Dictionary<string, object>(additionalSettings);
        if (tokenEndpointAuthMethod is not null)
            settings["token_endpoint_auth_method"] = tokenEndpointAuthMethod;
        if (grantTypes.Count > 0)
            settings["grant_types_supported"] = grantTypes;

        await AddClientAsync(
            serviceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(settings),
            [persistedSecret],
            cancellationToken
        );
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client whose registered secret is the public half of
    /// <paramref name="certificate"/> (stored as an <c>x509</c> secret), so the client can authenticate via
    /// <c>private_key_jwt</c> by signing a <c>client_assertion</c> with the certificate's private key.
    /// </summary>
    public async ValueTask SeedConfidentialClientWithCertificateAsync(
        string clientId,
        X509Certificate2 certificate,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var persistedSecret = CreateCertificateSecret(serviceProvider, clientId, certificate);

        var settings = new Dictionary<string, object>
        {
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .PrivateKeyJwt,
        };
        if (grantTypes.Count > 0)
            settings["grant_types_supported"] = grantTypes;

        await AddClientAsync(
            serviceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(settings),
            [persistedSecret],
            cancellationToken
        );
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential <c>private_key_jwt</c> client whose registered key is the
    /// public half of <paramref name="certificate"/>, registered with the given redirect URIs and the extra client
    /// settings, so a security profile's authorization flow can be exercised end to end.
    /// </summary>
    public async ValueTask SeedPrivateKeyJwtClientWithSettingsAsync(
        string clientId,
        X509Certificate2 certificate,
        IReadOnlyDictionary<string, object> settings,
        IReadOnlyList<string> redirectUris,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var persistedSecret = CreateCertificateSecret(serviceProvider, clientId, certificate);

        var allSettings = new Dictionary<string, object>(settings)
        {
            ["redirect_uris"] = redirectUris,
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .PrivateKeyJwt,
        };

        await AddClientAsync(
            serviceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(allSettings),
            [persistedSecret],
            cancellationToken
        );
    }

    private static PersistedSecret CreateCertificateSecret(
        IServiceProvider serviceProvider,
        string clientId,
        X509Certificate2 certificate
    )
    {
        // Only the public certificate is registered, stored unencrypted; the server verifies assertions with it.
        var certificateBytes = certificate.Export(X509ContentType.Cert);

        using var publicKey = certificate.GetRSAPublicKey();
        var keySizeBits = publicKey!.KeySize;

        return new PersistedSecretBuilder
        {
            SecretId = $"{clientId}-cert",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(100),
            RetiresWhen = DateTimeOffset.UnixEpoch.AddYears(100),
            // A certificate is persisted as its crypto family plus the x509 encoding (ADR-1104).
            SecretType = SecretTypes.Rsa,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.X509,
            KeySizeBits = keySizeBits,
            EncodedValue = Base64Url.EncodeToString(certificateBytes),
        }.Build();
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client whose public verification keys are published at the
    /// given <paramref name="jwksUri"/> (resolved at runtime), with no stored secrets, so the client can authenticate
    /// via <c>private_key_jwt</c> by signing a <c>client_assertion</c> with the corresponding private key.
    /// </summary>
    public ValueTask SeedConfidentialClientWithJwksUriAsync(
        string clientId,
        string jwksUri,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    ) =>
        SeedClientWithJwksUriAsync(
            clientId,
            jwksUri,
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
            grantTypes,
            cancellationToken
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>self_signed_tls_client_auth</c>
    /// whose certificates are published, as the <c>x5c</c> of its keys, at the given <paramref name="jwksUri"/>
    /// (resolved at runtime), with no stored secrets (RFC 8705 §2.2).
    /// </summary>
    public ValueTask SeedSelfSignedTlsClientWithJwksUriAsync(
        string clientId,
        string jwksUri,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    ) =>
        SeedClientWithJwksUriAsync(
            clientId,
            jwksUri,
            OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
            grantTypes,
            cancellationToken
        );

    private async ValueTask SeedClientWithJwksUriAsync(
        string clientId,
        string jwksUri,
        string authenticationMethod,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        var settings = new Dictionary<string, object>
        {
            ["jwks_uri"] = jwksUri,
            ["token_endpoint_auth_method"] = authenticationMethod,
        };
        if (grantTypes.Count > 0)
            settings["grant_types_supported"] = grantTypes;

        var settingsJson = System.Text.Json.JsonSerializer.SerializeToElement(settings);

        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var persistedClient = new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = clientId,
            Settings = settingsJson,
        }.Build();

        await store.AddAsync(persistedClient, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>self_signed_tls_client_auth</c>
    /// (RFC 8705 §2.2) whose registered certificate is the public half of <paramref name="certificate"/>, so the
    /// client authenticates by presenting that certificate on the mutual-TLS connection.
    /// </summary>
    public ValueTask SeedSelfSignedTlsClientAsync(
        string clientId,
        X509Certificate2 certificate,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    ) =>
        SeedSelfSignedTlsClientAsync(
            clientId,
            certificate,
            new Dictionary<string, object>(),
            grantTypes,
            cancellationToken
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>self_signed_tls_client_auth</c>
    /// whose registered certificate is the public half of <paramref name="certificate"/>, carrying the given
    /// additional client settings (for example <c>require_tls_client_certificate_bound_access_tokens</c>).
    /// </summary>
    public async ValueTask SeedSelfSignedTlsClientAsync(
        string clientId,
        X509Certificate2 certificate,
        IReadOnlyDictionary<string, object> additionalSettings,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var certificateBytes = certificate.Export(X509ContentType.Cert);

        using var publicKey = certificate.GetRSAPublicKey();
        var keySizeBits = publicKey!.KeySize;

        var persistedSecret = new PersistedSecretBuilder
        {
            SecretId = $"{clientId}-cert",
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(100),
            RetiresWhen = DateTimeOffset.UnixEpoch.AddYears(100),
            SecretType = SecretTypes.Rsa,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.X509,
            KeySizeBits = keySizeBits,
            EncodedValue = Base64Url.EncodeToString(certificateBytes),
        }.Build();

        var settings = new Dictionary<string, object>(additionalSettings)
        {
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .SelfSignedTlsClientAuth,
        };
        if (grantTypes.Count > 0)
            settings["grant_types_supported"] = grantTypes;

        await AddClientAsync(
            serviceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(settings),
            [persistedSecret],
            cancellationToken
        );
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>tls_client_auth</c>
    /// (RFC 8705 §2.1) whose expected certificate subject distinguished name is <paramref name="subjectDn"/> and whose
    /// certificate must chain to one of <paramref name="trustAnchors"/> (PEM), so the client authenticates by
    /// presenting a certificate issued by that PKI with that subject on the mutual-TLS connection.
    /// </summary>
    public ValueTask SeedTlsClientAuthSubjectDnClientAsync(
        string clientId,
        string subjectDn,
        IReadOnlyCollection<string> trustAnchors,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    ) =>
        SeedTlsClientAuthSubjectDnClientAsync(
            clientId,
            subjectDn,
            trustAnchors,
            new Dictionary<string, object>(),
            grantTypes,
            cancellationToken
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>tls_client_auth</c> whose expected
    /// certificate subject distinguished name is <paramref name="subjectDn"/> and whose certificate must chain to one
    /// of <paramref name="trustAnchors"/> (PEM), carrying the given additional client settings (for example
    /// <c>tls_client_auth_revocation_mode</c>), which override the defaults this method sets.
    /// </summary>
    public async ValueTask SeedTlsClientAuthSubjectDnClientAsync(
        string clientId,
        string subjectDn,
        IReadOnlyCollection<string> trustAnchors,
        IReadOnlyDictionary<string, object> additionalSettings,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        var settings = new Dictionary<string, object>
        {
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .TlsClientAuth,
            ["tls_client_auth_subject_dn"] = subjectDn,
            ["tls_client_auth_trust_anchors"] = trustAnchors,
            // By default the test PKI publishes no revocation data.
            ["tls_client_auth_revocation_mode"] = OpenIdConstants
                .ClientCertificateRevocationMode
                .None,
        };
        foreach (var (name, value) in additionalSettings)
            settings[name] = value;
        if (grantTypes.Count > 0)
            settings["grant_types_supported"] = grantTypes;

        using var scope = Services.CreateScope();
        await AddClientAsync(
            scope.ServiceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(settings),
            [],
            cancellationToken
        );
    }

    private static async ValueTask AddClientAsync(
        IServiceProvider serviceProvider,
        string clientId,
        System.Text.Json.JsonElement settingsJson,
        IReadOnlyList<PersistedSecret> secrets,
        CancellationToken cancellationToken
    )
    {
        var storeManagerFactory = serviceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IClientStore>();

        var persistedClient = new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = clientId,
            Settings = settingsJson,
            Secrets = secrets,
        }.Build();

        await store.AddAsync(persistedClient, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential <c>client_secret_post</c> client that selects the
    /// <paramref name="securityProfiles"/> (for example <c>fapi2</c>), so profile-driven setting ceilings and
    /// enforcement can be exercised end-to-end. An empty list is stored as an explicit empty selection.
    /// </summary>
    public async ValueTask SeedConfidentialClientWithSecurityProfilesAsync(
        string clientId,
        string clientSecret,
        IReadOnlyCollection<string> securityProfiles,
        IReadOnlyList<string> grantTypes,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var persistedSecret = await CreateSharedSecretAsync(
            serviceProvider,
            $"{clientId}-secret",
            clientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            cancellationToken
        );

        var settings = new Dictionary<string, object>
        {
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .ClientSecretPost,
            ["security_profiles"] = securityProfiles,
        };
        if (grantTypes.Count > 0)
            settings["grant_types_supported"] = grantTypes;

        await AddClientAsync(
            serviceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(settings),
            [persistedSecret],
            cancellationToken
        );
    }

    /// <summary>
    /// Seeds a resource server owning the given scopes and a client grant authorizing the client to all of them,
    /// so the client may request those scopes.
    /// </summary>
    public ValueTask SeedResourceServerWithClientGrantAsync(
        string clientId,
        string resourceServerId,
        string identifier,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken
    ) =>
        SeedResourceServerWithClientGrantAsync(
            clientId,
            resourceServerId,
            identifier,
            new Dictionary<string, object>(),
            scopes,
            cancellationToken
        );

    /// <summary>
    /// Seeds a resource server carrying the given settings document and owning the given scopes, and a client grant
    /// authorizing the client to all of them. The settings bound every access token issued for the resource server.
    /// </summary>
    public async ValueTask SeedResourceServerWithClientGrantAsync(
        string clientId,
        string resourceServerId,
        string identifier,
        IReadOnlyDictionary<string, object> settings,
        IReadOnlyList<string> scopes,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        var settingsJson = System.Text.Json.JsonSerializer.SerializeToElement(settings);

        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();
        var clientGrantStore = storeManager.GetStore<IClientGrantStore>();

        var resourceServer = new PersistedResourceServerBuilder
        {
            TenantId = TenantId,
            ResourceServerId = resourceServerId,
            Identifier = identifier,
            Name = resourceServerId,
            Settings = settingsJson,
            Scopes = scopes
                .Select(value => new PersistedScope
                {
                    Value = value,
                    Description = null,
                    IsSystem = false,
                })
                .ToList(),
        }.Build();
        await resourceServerStore.AddAsync(resourceServer, cancellationToken);
        // Persist the resource server before the grant: the grant store resolves the resource-server foreign key,
        // and an in-memory query does not observe unsaved inserts.
        await storeManager.SaveChangesAsync(cancellationToken);

        await clientGrantStore.AddAsync(
            new PersistedClientGrant
            {
                TenantId = TenantId,
                ClientId = clientId,
                ResourceServerId = resourceServerId,
                ConcurrencyToken = string.Empty,
                Scopes = scopes,
            },
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Directly persists the tenant and a confidential client whose stored secret is protected from
    /// <paramref name="storedSecret"/>, without an HTTP warm-up that would let a configured bootstrap seeder create
    /// the client first. Use this to stage a pre-existing client (for example with a stale secret) that a later
    /// request's provisioning pass must reconcile. The client registers no client-authentication method, so it uses the
    /// default, <c>client_secret_basic</c>.
    /// </summary>
    public async ValueTask SeedClientDirectlyAsync(
        string clientId,
        string storedSecret,
        CancellationToken cancellationToken
    )
    {
        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var storeManagerFactory = serviceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);

        await EnsureTeamAsync(cancellationToken);
        var tenantStore = storeManager.GetStore<ITenantStore>();
        if (await tenantStore.GetOrDefaultAsync(TenantId, cancellationToken) is null)
        {
            await tenantStore.AddAsync(CreateEmptyTenant(TenantId), cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }

        var persistedSecret = await CreateSharedSecretAsync(
            serviceProvider,
            $"{clientId}-secret",
            storedSecret,
            tokenEndpointAuthMethod: null,
            cancellationToken
        );

        var persistedClient = new PersistedClientBuilder
        {
            TenantId = TenantId,
            ClientId = clientId,
            Secrets = [persistedSecret],
        }.Build();

        var clientStore = storeManager.GetStore<IClientStore>();
        await clientStore.AddAsync(persistedClient, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Ensures the tenant exists and seeds an enabled local account whose stored password hash matches
    /// <paramref name="password"/>, so the resource-owner <c>password</c> grant can be exercised. Returns the
    /// generated <c>LocalAccountId</c> (the subject of the account's self-issued federated identity).
    /// </summary>
    public ValueTask<string> SeedLocalAccountAsync(
        string userName,
        string password,
        CancellationToken cancellationToken
    ) =>
        SeedLocalAccountAsync(
            userName,
            password,
            email: null,
            emailVerified: false,
            cancellationToken
        );

    /// <summary>
    /// Ensures the tenant exists and seeds an enabled local account carrying the given email address, whose stored
    /// password hash matches <paramref name="password"/>. Returns the generated <c>LocalAccountId</c> (the subject of
    /// the account's self-issued federated identity).
    /// </summary>
    public async ValueTask<string> SeedLocalAccountAsync(
        string userName,
        string password,
        string? email,
        bool emailVerified,
        CancellationToken cancellationToken
    )
    {
        await EnsureTenantPersistedAsync(cancellationToken);

        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var localAccountId = Guid.NewGuid().ToString("N");

        var storeManagerFactory = serviceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<ILocalAccountStore>();

        // The store hashes the credential and generates the security stamp internally.
        await store.AddAsync(
            new PersistedLocalAccount
            {
                TenantId = TenantId,
                LocalAccountId = localAccountId,
                UserName = userName,
                Email = email,
                EmailVerified = emailVerified,
                IsEnabled = true,
                Claims = [],
                ConcurrencyToken = string.Empty,
            },
            SecureEncoding.UTF8.GetBytes(password),
            cancellationToken
        );
        await storeManager.SaveChangesAsync(cancellationToken);

        return localAccountId;
    }

    /// <summary>
    /// Persists the tenant with the given settings and no keys, so the tenant's keys are the ones the host provisions
    /// when it first seeds the tenant (<c>OpenId:Tenant:KeyProvisioning</c>). The tenant reads its settings and keys when
    /// it is first served, so this must be called before the
    /// factory serves any request.
    /// </summary>
    /// <param name="settings">The tenant's settings document.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async ValueTask SeedTenantSettingsAsync(
        IReadOnlyDictionary<string, object> settings,
        CancellationToken cancellationToken
    )
    {
        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var storeManagerFactory = serviceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var tenantStore = storeManager.GetStore<ITenantStore>();
        if (await tenantStore.GetOrDefaultAsync(TenantId, cancellationToken) is not null)
        {
            throw new InvalidOperationException(
                "The tenant is already persisted, so it may already be served with its earlier settings."
            );
        }

        await EnsureTeamAsync(cancellationToken);
        var tenant = CreateEmptyTenant(TenantId);
        tenant.Settings.Value = System.Text.Json.JsonSerializer.SerializeToElement(settings);
        await tenantStore.AddAsync(tenant, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Replaces the settings document that the store holds for the tenant. It writes through the store, without the
    /// management API's checks and without evicting the tenant that the host materialized, so a management request
    /// reads a document the settings vocabulary may not honor while the caller's token still validates.
    /// </summary>
    /// <param name="settings">The tenant's settings document.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    public async ValueTask ReplaceTenantSettingsAsync(
        IReadOnlyDictionary<string, object> settings,
        CancellationToken cancellationToken
    )
    {
        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var storeManagerFactory = serviceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var tenantStore = storeManager.GetStore<ITenantStore>();
        var tenantSettings =
            await tenantStore.GetSettingsOrDefaultAsync(TenantId, cancellationToken)
            ?? throw new InvalidOperationException(
                "The tenant is not persisted yet; serve a request first."
            );

        tenantSettings.Value = System.Text.Json.JsonSerializer.SerializeToElement(settings);
        await tenantStore.UpdateSettingsAsync(tenantSettings, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Creates an HTTP client that calls the management API as the given client: it obtains a
    /// <c>client_credentials</c> access token with the <c>read:clients</c> scope, then sends it as a bearer token.
    /// </summary>
    /// <param name="clientId">The identifier of the client that authenticates with a shared secret.</param>
    /// <param name="clientSecret">The client's secret, as sent to the token endpoint.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The HTTP client, which does not follow redirects and carries the bearer token.</returns>
    public ValueTask<HttpClient> CreateManagementClientAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken
    ) =>
        new ManagementTokenRequest(clientId, clientSecret).CreateClientAsync(
            this,
            cancellationToken
        );

    /// <summary>
    /// Creates an HTTP client that calls the management API as an end-user: it signs the user in through the
    /// interactive login page, runs the authorization-code flow with PKCE <c>S256</c> as the given public client, then
    /// sends the resulting access token as a bearer token.
    /// </summary>
    /// <param name="clientId">The identifier of a public client registered with <paramref name="redirectUri"/>.</param>
    /// <param name="redirectUri">The redirect URI the client is registered with.</param>
    /// <param name="userName">The user name of a seeded local account.</param>
    /// <param name="password">The password of the account.</param>
    /// <param name="scope">The space-delimited scopes the authorization request asks for.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The HTTP client, which does not follow redirects and carries the bearer token.</returns>
    public async ValueTask<HttpClient> CreateManagementUserClientAsync(
        string clientId,
        string redirectUri,
        string userName,
        string password,
        string scope,
        CancellationToken cancellationToken
    )
    {
        using var browser = CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var sessionCookie = await InteractiveLogin.SignInAsync(
            browser,
            userName,
            password,
            cancellationToken
        );

        var codeVerifier = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
        var codeChallenge = Base64Url.EncodeToString(
            SHA256.HashData(SecureEncoding.UTF8.GetBytes(codeVerifier))
        );
        var state = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(16));

        var authorizeParameters = new Dictionary<string, string>
        {
            ["response_type"] = OpenIdProtocol.ResponseTypes.Code,
            ["client_id"] = clientId,
            ["redirect_uri"] = redirectUri,
            ["scope"] = scope,
            ["state"] = state,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
        };
        var authorizeQuery = string.Join(
            '&',
            authorizeParameters.Select(pair =>
                $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"
            )
        );
        using var authorizeRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/oauth2/authorize?{authorizeQuery}"
        );
        authorizeRequest.Headers.Add(
            "Cookie",
            $"{InteractiveLogin.SessionCookieName}={sessionCookie}"
        );
        using var authorizeResponse = await browser.SendAsync(authorizeRequest, cancellationToken);

        var location = authorizeResponse.Headers.Location;
        if (
            location is null
            || !location.OriginalString.StartsWith(redirectUri, StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                $"The authorization request was not redirected to '{redirectUri}': {(int)authorizeResponse.StatusCode} {location}"
            );
        }

        var code = System.Web.HttpUtility.ParseQueryString(location.Query)["code"];
        if (string.IsNullOrEmpty(code))
        {
            throw new InvalidOperationException(
                $"The authorization response carries no code: {location}"
            );
        }

        var client = CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        try
        {
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
            {
                Content = new FormUrlEncodedContent(
                    new Dictionary<string, string>
                    {
                        ["grant_type"] = OpenIdProtocol.GrantTypes.AuthorizationCode,
                        ["code"] = code,
                        ["redirect_uri"] = redirectUri,
                        ["client_id"] = clientId,
                        ["code_verifier"] = codeVerifier,
                    }
                ),
            };

            using var response = await client.SendAsync(tokenRequest, cancellationToken);
            response.EnsureSuccessStatusCode();
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonElement = System.Text.Json.JsonElement.Parse(body);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
                "Bearer",
                jsonElement.GetProperty("access_token").GetString()
            );
            return client;
        }
        catch
        {
            client.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Creates the <see cref="TeamId"/> team when it does not exist, so a test that persists a tenant directly has the
    /// team the tenant belongs to.
    /// </summary>
    public async ValueTask EnsureTeamAsync(CancellationToken cancellationToken)
    {
        using var scope = Services.CreateScope();
        await scope
            .ServiceProvider.GetRequiredService<ITeamProvisioner>()
            .EnsureCreatedAsync(TeamId, TeamId, cancellationToken);
    }

    private static PersistedTenant CreateEmptyTenant(string tenantId) =>
        new PersistedTenantBuilder
        {
            TenantId = tenantId,
            TeamId = TeamId,
            Issuer = TestServerIssuer,
            Settings = System.Text.Json.JsonSerializer.SerializeToElement(null, typeof(object)),
        }.Build();

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Resolve the content root to the host project so its appsettings.json is loaded.
        builder.UseSolutionRelativeContentRoot("hosts/NCode.Identity.OpenId.Host");
        builder.UseEnvironment(Microsoft.Extensions.Hosting.Environments.Development);

        // A tenant is served only beneath its issuer, so the static tenant is pinned to the TestServer's own origin,
        // the way a deployment pins its public one. A derived factory that changes the tenant path or strategy layers
        // its own issuer over this one.
        builder.ConfigureAppConfiguration(configurationBuilder =>
            configurationBuilder.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["OpenId:TenantResolution:StaticSingle:Issuer"] = TestServerIssuer,
                    ["SecretProtection:RootKeyProvider"] = "local",
                    ["SecretProtection:RootKeyProviders:Local:Key"] = RootKey,
                }
            )
        );

        builder.ConfigureTestServices(serviceCollection =>
        {
            // Replace the host's shared in-memory database with a per-factory isolated one.
            serviceCollection.RemoveAll<DbContextOptions<OpenIdDbContext>>();
            serviceCollection.RemoveAll<IDbContextFactory<OpenIdDbContext>>();

            serviceCollection.AddDbContextFactory<OpenIdDbContext>(options =>
                options.UseInMemoryDatabase(DatabaseName, DatabaseRoot)
            );

            // The key ring lives beside the master key that protects it, shared by every host of this factory, and no
            // test reads or writes the machine's key directory.
            serviceCollection.RemoveAll<DbContextOptions<DeveloperDataProtectionDbContext>>();
            serviceCollection.AddDbContext<DeveloperDataProtectionDbContext>(options =>
                options.UseInMemoryDatabase(KeyRingDatabaseName, DatabaseRoot)
            );
            serviceCollection
                .AddDataProtection()
                .PersistKeysToDbContext<DeveloperDataProtectionDbContext>();

            // Widen the platform ceiling (registered after the library default, so it unions on top):
            // enables the client_credentials and resource-owner password grants plus a custom "api" scope for
            // integration coverage. Both grants stay off in the host's own secure defaults.
            serviceCollection.AddSingleton<
                IDefaultSettingsProvider,
                TestPlatformSettingsProvider
            >();

            // A supplied client_secret is stored as a PBKDF2 verifier whose work factor would make every seeded
            // client's token request cost what a password check costs; a verifier records its own count, so a low one
            // exercises the same path faster. See ADR-1391.
            serviceCollection.Configure<Pbkdf2SecretVerifierOptions>(options =>
                options.Iterations = 1_000
            );

            // Seed no developer clients unless a derived factory opts in (the last registration wins, so the
            // developer seeder sees an empty set of clients).
            if (!SeedsDeveloperClients)
            {
                serviceCollection.AddSingleton(new DeveloperInteractiveSeedOptions());
            }

            // The stubs replace the SSRF-guarded client's primary handler (GuardedHttpClient.Name, ADR-1315), so a fetch or
            // delivery runs without a real outbound connection, which the guard would refuse.
            if (
                ClientJwksResponder is not null
                || CertificateRevocationListResponder is not null
                || BackChannelLogoutExchanges is not null
            )
            {
                if (
                    ClientJwksResponder is not null
                    && CertificateRevocationListResponder is not null
                )
                {
                    throw new InvalidOperationException(
                        "A host stubs either jwks_uri or revocation fetches, not both: both are GETs on the one guarded client."
                    );
                }

                var clientJwksResponder = ClientJwksResponder;
                var crlResponder = CertificateRevocationListResponder;
                var backChannelLogoutExchanges = BackChannelLogoutExchanges;

                // The factory disposes a primary handler when it rotates it, and the stub disposes its inner handlers,
                // so each rotation builds its own.
                StubOutboundHttpMessageHandler CreateStub()
                {
                    HttpMessageHandler? getHandler = null;
                    if (clientJwksResponder is not null)
                        getHandler = new StubJsonHttpMessageHandler(clientJwksResponder);
                    else if (crlResponder is not null)
                        getHandler = new StubBinaryHttpMessageHandler(crlResponder);

                    HttpMessageHandler? postHandler = null;
                    if (backChannelLogoutExchanges is not null)
                        postHandler = new StubRecordingHttpMessageHandler(
                            backChannelLogoutExchanges
                        );

                    return new StubOutboundHttpMessageHandler(getHandler, postHandler);
                }

                serviceCollection
                    .AddHttpClient("NCode.Identity.OpenId.GuardedHttp")
                    .ConfigurePrimaryHttpMessageHandler(CreateStub);
            }

            // The reconciliation service materializes and persists the platform when the host starts, so a persisted
            // platform document is seeded from the host's own lifecycle, before any hosted service starts.
            if (PersistedPlatformSettings is { } persistedPlatformSettings)
            {
                serviceCollection.AddSingleton<Microsoft.Extensions.Hosting.IHostedService>(
                    provider => new PersistedPlatformSeedService(
                        provider.GetRequiredService<IServiceScopeFactory>(),
                        persistedPlatformSettings
                    )
                );
            }

            // The TestServer terminates no TLS, so mutual-TLS client authentication is exercised by forwarding the
            // client certificate in a header (the reverse-proxy path the default resolver already supports).
            serviceCollection.Configure<MutualTlsForwardingOptions>(options =>
            {
                options.CertificateHeaderName = MutualTlsCertificateHeaderName;
                options.Format = ClientCertificateHeaderFormats.Base64Der;
            });
        });
    }

    /// <inheritdoc />
    protected override Microsoft.Extensions.Hosting.IHost CreateHost(
        Microsoft.Extensions.Hosting.IHostBuilder builder
    ) => new ApplicationRunLoopHost(base.CreateHost(builder));
}
