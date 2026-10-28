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
using NCode.Identity.OpenId.Host;
using NCode.Identity.OpenId.Host.Database;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.EntityFramework;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.IntegrationTests.Infrastructure;

/// <summary>
/// A <see cref="WebApplicationFactory{TEntryPoint}"/> that boots the OpenID <c>Host</c> project in-process
/// through the full ASP.NET Core HTTP pipeline (via <see cref="TestServer"/>), providing an
/// <see cref="System.Net.Http.HttpClient"/> for over-the-wire integration tests.
/// </summary>
/// <remarks>
/// Each factory instance uses its own uniquely-named in-memory database so tests are fully isolated
/// (EF Core caches its internal service provider by option configuration, so a shared database name
/// would cause parallel test hosts to collide). At present the tenant has no signing keys; once an
/// ephemeral development key composition exists, this factory (or a derived one) can apply it so that
/// token-signing and a non-empty JWKS can be asserted. A host is stopped through its own run loop
/// (<see cref="ApplicationRunLoopHost"/>), so each hosted service is stopped once.
/// </remarks>
public class HostApplicationFactory : WebApplicationFactory<HostApiMarker>
{
    /// <summary>The tenant id used by the host's static single-tenant provider.</summary>
    public const string TenantId = "default";

    /// <summary>
    /// The issuer pinned on the static tenant: the origin <see cref="Microsoft.AspNetCore.TestHost.TestServer"/> serves
    /// requests under, so the URLs the server publishes are ones a test client can call.
    /// </summary>
    public const string TestServerIssuer = "http://localhost";

    /// <summary>The header the test host reads a forwarded mutual-TLS client certificate (base64 DER) from.</summary>
    public const string MutualTlsCertificateHeaderName = "X-Client-Cert";

    /// <summary>
    /// When set, overrides the SSRF-guarded client <c>jwks_uri</c> HTTP client with a stub that serves the JSON this
    /// responder returns, so a <c>jwks_uri</c> fetch can be exercised without a real network connection.
    /// </summary>
    public Func<HttpRequestMessage, string>? ClientJwksResponder { get; set; }

    /// <summary>
    /// When set, overrides the SSRF-guarded certificate revocation HTTP client with a stub that serves the DER bytes
    /// this responder returns, so a CRL fetch can be exercised without a real network connection.
    /// </summary>
    public Func<HttpRequestMessage, byte[]>? CertificateRevocationListResponder { get; set; }

    /// <summary>
    /// Gets a value indicating whether the developer clients of the Development overlay are seeded. They are seeded
    /// only when a derived factory returns <see langword="true"/>, because they are public clients with <c>http</c>
    /// redirect URIs that a test narrowing the server's or the tenant's ceilings would make incoherent.
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
    /// Persists the server's own settings document. The server is materialized once, by the first request that needs
    /// it, and loads this document then, so this must be called before the factory serves any request.
    /// </summary>
    /// <param name="settings">The server's settings document.</param>
    public async Task SeedServerSettingsAsync(IReadOnlyDictionary<string, object> settings)
    {
        const string serverId = OpenIdServerOptions.DefaultServerId;

        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<IServerStore>();

        if (await store.GetOrDefaultAsync(serverId, CancellationToken.None) is not null)
        {
            throw new InvalidOperationException(
                "The server is already persisted, so it may already be materialized with its earlier settings."
            );
        }

        var persistedServer = new PersistedServer
        {
            ServerId = serverId,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            DisplayName = serverId,
            Settings = new PersistedServerSettings
            {
                ServerId = serverId,
                ConcurrencyToken = Guid.NewGuid().ToString("N"),
                Value = System.Text.Json.JsonSerializer.SerializeToElement(settings),
            },
            Secrets = new PersistedServerSecrets
            {
                ServerId = serverId,
                ConcurrencyToken = Guid.NewGuid().ToString("N"),
                Value = [],
            },
        };

        await store.AddAsync(persistedServer, CancellationToken.None);
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Ensures the tenant exists (persisted lazily on first request) and seeds a public client
    /// (<c>none</c> client authentication, no secrets) registered with the given redirect URIs.
    /// </summary>
    public async Task SeedPublicClientAsync(string clientId, params string[] redirectUris)
    {
        // A first request forces the static tenant provider to persist the tenant entity,
        // which ClientStore.AddAsync requires as a foreign key.
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        var settingsJson = System.Text.Json.JsonSerializer.SerializeToElement(
            new Dictionary<string, object>
            {
                ["redirect_uris"] = redirectUris,
                ["token_endpoint_auth_method"] = OpenIdProtocol.ClientAuthenticationMethods.None,
            }
        );

        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<IClientStore>();

        var persistedClient = new PersistedClient
        {
            TenantId = TenantId,
            ClientId = clientId,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = clientId,
                Value = settingsJson,
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = clientId,
                Value = [],
            },
        };

        await store.AddAsync(persistedClient, CancellationToken.None);
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a public client registered with the given redirect URIs and the extra
    /// client settings.
    /// </summary>
    public async Task SeedPublicClientWithSettingsAsync(
        string clientId,
        IReadOnlyDictionary<string, object> settings,
        params string[] redirectUris
    )
    {
        // A first request forces the static tenant provider to persist the tenant entity,
        // which ClientStore.AddAsync requires as a foreign key.
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

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
            []
        );
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a passwordless local account together with the one-to-one self-issued
    /// federated identity that a real interactive login provisions (ADR-0035), so the UserInfo scope-claims enricher
    /// can be exercised end-to-end. Returns the provisioned principal id — the value a self-issued access token carries
    /// as its public <c>sub</c>, which the enricher resolves the local account from.
    /// </summary>
    public async Task<string> SeedLocalAccountAsync(
        string localAccountId,
        string? email = null,
        bool emailVerified = false,
        params (string Type, string Value)[] claims
    )
    {
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        var principalId = $"pid-{Guid.NewGuid():N}";

        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );

        var accountStore = storeManager.GetStore<ILocalAccountStore>();
        await accountStore.AddAsync(
            new PersistedLocalAccount
            {
                TenantId = TenantId,
                LocalAccountId = localAccountId,
                UserName = localAccountId,
                Email = email,
                EmailVerified = emailVerified,
                IsEnabled = true,
                Claims = claims
                    .Select(c => new PersistedLocalAccountClaim { Type = c.Type, Value = c.Value })
                    .ToList(),
                ConcurrencyToken = string.Empty,
            },
            password: null,
            CancellationToken.None
        );

        var principalStore = storeManager.GetStore<IFederatedPrincipalStore>();
        await principalStore.AddAsync(
            new PersistedFederatedPrincipal
            {
                TenantId = TenantId,
                PrincipalId = principalId,
                ConcurrencyToken = string.Empty,
            },
            CancellationToken.None
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
            CancellationToken.None
        );

        await storeManager.SaveChangesAsync(CancellationToken.None);
        return principalId;
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>client_secret_post</c> whose
    /// symmetric secret matches <paramref name="clientSecret"/>, so the <c>client_credentials</c> grant can be
    /// exercised. When <paramref name="grantTypes"/> is supplied, the client's effective <c>grant_types_supported</c>
    /// setting is restricted to those values (required for grants a client must explicitly allow, such as
    /// <c>password</c>).
    /// </summary>
    public Task SeedConfidentialClientAsync(
        string clientId,
        string clientSecret,
        params string[] grantTypes
    ) =>
        SeedSharedSecretClientAsync(
            clientId,
            clientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            grantTypes
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>client_secret_basic</c> whose
    /// symmetric secret matches <paramref name="clientSecret"/>, so it authenticates with HTTP Basic authentication.
    /// When <paramref name="grantTypes"/> is supplied, the client's effective <c>grant_types_supported</c> setting is
    /// restricted to those values.
    /// </summary>
    public Task SeedClientSecretBasicClientAsync(
        string clientId,
        string clientSecret,
        params string[] grantTypes
    ) =>
        SeedSharedSecretClientAsync(
            clientId,
            clientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            grantTypes
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>client_secret_jwt</c> whose
    /// symmetric secret matches <paramref name="clientSecret"/>, so it authenticates with a <c>client_assertion</c> it
    /// signs with that secret. When <paramref name="grantTypes"/> is supplied, the client's effective
    /// <c>grant_types_supported</c> setting is restricted to those values.
    /// </summary>
    public Task SeedClientSecretJwtClientAsync(
        string clientId,
        string clientSecret,
        params string[] grantTypes
    ) =>
        SeedSharedSecretClientAsync(
            clientId,
            clientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt,
            grantTypes
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client that registers no client-authentication method, so it
    /// uses the default (<c>client_secret_basic</c>), with a symmetric secret matching <paramref name="clientSecret"/>.
    /// When <paramref name="grantTypes"/> is supplied, the client's effective <c>grant_types_supported</c> setting is
    /// restricted to those values.
    /// </summary>
    public Task SeedClientWithoutAuthenticationMethodAsync(
        string clientId,
        string clientSecret,
        params string[] grantTypes
    ) => SeedSharedSecretClientAsync(clientId, clientSecret, null, grantTypes);

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for the shared-secret
    /// <paramref name="tokenEndpointAuthMethod"/> (<c>client_secret_basic</c> or <c>client_secret_post</c>) whose
    /// symmetric secret matches <paramref name="clientSecret"/>, registered with the given redirect URIs and the extra
    /// client settings, so an authorization-code flow can be exercised end to end.
    /// </summary>
    public Task SeedSharedSecretClientWithSettingsAsync(
        string clientId,
        string clientSecret,
        string tokenEndpointAuthMethod,
        IReadOnlyDictionary<string, object> settings,
        params string[] redirectUris
    ) =>
        SeedSharedSecretClientAsync(
            clientId,
            clientSecret,
            tokenEndpointAuthMethod,
            [],
            new Dictionary<string, object>(settings) { ["redirect_uris"] = redirectUris }
        );

    private async Task SeedSharedSecretClientAsync(
        string clientId,
        string clientSecret,
        string? tokenEndpointAuthMethod,
        string[] grantTypes,
        IReadOnlyDictionary<string, object>? additionalSettings = null
    )
    {
        // A first request forces the static tenant provider to persist the tenant entity,
        // which ClientStore.AddAsync requires as a foreign key.
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        // Persist the given client_secret through the same shared-secret primitive production uses (ADR-0110), protected
        // under the tenant's master key, so the stored UTF-8 shape is identical to a generated secret's and client
        // authentication round-trips.
        var protector = await serviceProvider
            .GetRequiredService<ISecretProtectorProvider>()
            .GetProtectorAsync(SecretOwner.ForTenant(TenantId), [], CancellationToken.None);

        var persistedSecret = serviceProvider
            .GetRequiredService<ISecretGenerator>()
            .ImportSharedSecret(
                protector,
                new ImportSharedSecretRequest
                {
                    SecretId = $"{clientId}-secret",
                    Use = null,
                    Algorithm = null,
                    CreatedWhen = DateTimeOffset.UnixEpoch,
                    ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(100),
                },
                SecureEncoding.UTF8.GetBytes(clientSecret)
            );

        var settings = additionalSettings is null
            ? new Dictionary<string, object>()
            : new Dictionary<string, object>(additionalSettings);
        if (tokenEndpointAuthMethod is not null)
            settings["token_endpoint_auth_method"] = tokenEndpointAuthMethod;
        if (grantTypes.Length > 0)
            settings["grant_types_supported"] = grantTypes;

        await AddClientAsync(
            serviceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(settings),
            [persistedSecret]
        );
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client whose registered secret is the public half of
    /// <paramref name="certificate"/> (stored as an <c>x509</c> secret), so the client can authenticate via
    /// <c>private_key_jwt</c> by signing a <c>client_assertion</c> with the certificate's private key.
    /// </summary>
    public async Task SeedConfidentialClientWithCertificateAsync(
        string clientId,
        X509Certificate2 certificate,
        params string[] grantTypes
    )
    {
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var persistedSecret = CreateCertificateSecret(serviceProvider, clientId, certificate);

        var settings = new Dictionary<string, object>
        {
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .PrivateKeyJwt,
        };
        if (grantTypes.Length > 0)
            settings["grant_types_supported"] = grantTypes;

        await AddClientAsync(
            serviceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(settings),
            [persistedSecret]
        );
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential <c>private_key_jwt</c> client whose registered key is the
    /// public half of <paramref name="certificate"/>, registered with the given redirect URIs and the extra client
    /// settings, so a security profile's authorization flow can be exercised end to end.
    /// </summary>
    public async Task SeedPrivateKeyJwtClientWithSettingsAsync(
        string clientId,
        X509Certificate2 certificate,
        IReadOnlyDictionary<string, object> settings,
        params string[] redirectUris
    )
    {
        // A first request forces the static tenant provider to persist the tenant entity,
        // which ClientStore.AddAsync requires as a foreign key.
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

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
            [persistedSecret]
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

        return new PersistedSecret
        {
            SecretId = $"{clientId}-cert",
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(100),
            // A certificate is persisted as its crypto family plus the x509 encoding (ADR-0104).
            SecretType = SecretTypes.Rsa,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.X509,
            KeySizeBits = keySizeBits,
            EncodedValue = Base64Url.EncodeToString(certificateBytes),
        };
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client whose public verification keys are published at the
    /// given <paramref name="jwksUri"/> (resolved at runtime), with no stored secrets, so the client can authenticate
    /// via <c>private_key_jwt</c> by signing a <c>client_assertion</c> with the corresponding private key.
    /// </summary>
    public Task SeedConfidentialClientWithJwksUriAsync(
        string clientId,
        string jwksUri,
        params string[] grantTypes
    ) =>
        SeedClientWithJwksUriAsync(
            clientId,
            jwksUri,
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
            grantTypes
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>self_signed_tls_client_auth</c>
    /// whose certificates are published, as the <c>x5c</c> of its keys, at the given <paramref name="jwksUri"/>
    /// (resolved at runtime), with no stored secrets (RFC 8705 §2.2).
    /// </summary>
    public Task SeedSelfSignedTlsClientWithJwksUriAsync(
        string clientId,
        string jwksUri,
        params string[] grantTypes
    ) =>
        SeedClientWithJwksUriAsync(
            clientId,
            jwksUri,
            OpenIdProtocol.ClientAuthenticationMethods.SelfSignedTlsClientAuth,
            grantTypes
        );

    private async Task SeedClientWithJwksUriAsync(
        string clientId,
        string jwksUri,
        string authenticationMethod,
        string[] grantTypes
    )
    {
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        var settings = new Dictionary<string, object>
        {
            ["jwks_uri"] = jwksUri,
            ["token_endpoint_auth_method"] = authenticationMethod,
        };
        if (grantTypes.Length > 0)
            settings["grant_types_supported"] = grantTypes;

        var settingsJson = System.Text.Json.JsonSerializer.SerializeToElement(settings);

        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<IClientStore>();

        var persistedClient = new PersistedClient
        {
            TenantId = TenantId,
            ClientId = clientId,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = clientId,
                Value = settingsJson,
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = clientId,
                Value = [],
            },
        };

        await store.AddAsync(persistedClient, CancellationToken.None);
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>self_signed_tls_client_auth</c>
    /// (RFC 8705 §2.2) whose registered certificate is the public half of <paramref name="certificate"/>, so the
    /// client authenticates by presenting that certificate on the mutual-TLS connection.
    /// </summary>
    public Task SeedSelfSignedTlsClientAsync(
        string clientId,
        X509Certificate2 certificate,
        params string[] grantTypes
    ) =>
        SeedSelfSignedTlsClientAsync(
            clientId,
            certificate,
            new Dictionary<string, object>(),
            grantTypes
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>self_signed_tls_client_auth</c>
    /// whose registered certificate is the public half of <paramref name="certificate"/>, carrying the given
    /// additional client settings (for example <c>require_tls_client_certificate_bound_access_tokens</c>).
    /// </summary>
    public async Task SeedSelfSignedTlsClientAsync(
        string clientId,
        X509Certificate2 certificate,
        IReadOnlyDictionary<string, object> additionalSettings,
        params string[] grantTypes
    )
    {
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var certificateBytes = certificate.Export(X509ContentType.Cert);

        using var publicKey = certificate.GetRSAPublicKey();
        var keySizeBits = publicKey!.KeySize;

        var persistedSecret = new PersistedSecret
        {
            SecretId = $"{clientId}-cert",
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(100),
            SecretType = SecretTypes.Rsa,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.X509,
            KeySizeBits = keySizeBits,
            EncodedValue = Base64Url.EncodeToString(certificateBytes),
        };

        var settings = new Dictionary<string, object>(additionalSettings)
        {
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .SelfSignedTlsClientAuth,
        };
        if (grantTypes.Length > 0)
            settings["grant_types_supported"] = grantTypes;

        await AddClientAsync(
            serviceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(settings),
            [persistedSecret]
        );
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>tls_client_auth</c>
    /// (RFC 8705 §2.1) whose expected certificate subject distinguished name is <paramref name="subjectDn"/> and whose
    /// certificate must chain to one of <paramref name="trustAnchors"/> (PEM), so the client authenticates by
    /// presenting a certificate issued by that PKI with that subject on the mutual-TLS connection.
    /// </summary>
    public Task SeedTlsClientAuthSubjectDnClientAsync(
        string clientId,
        string subjectDn,
        IReadOnlyCollection<string> trustAnchors,
        params string[] grantTypes
    ) =>
        SeedTlsClientAuthSubjectDnClientAsync(
            clientId,
            subjectDn,
            trustAnchors,
            new Dictionary<string, object>(),
            grantTypes
        );

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential client registered for <c>tls_client_auth</c> whose expected
    /// certificate subject distinguished name is <paramref name="subjectDn"/> and whose certificate must chain to one
    /// of <paramref name="trustAnchors"/> (PEM), carrying the given additional client settings (for example
    /// <c>tls_client_auth_revocation_mode</c>), which override the defaults this method sets.
    /// </summary>
    public async Task SeedTlsClientAuthSubjectDnClientAsync(
        string clientId,
        string subjectDn,
        IReadOnlyCollection<string> trustAnchors,
        IReadOnlyDictionary<string, object> additionalSettings,
        params string[] grantTypes
    )
    {
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

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
        if (grantTypes.Length > 0)
            settings["grant_types_supported"] = grantTypes;

        using var scope = Services.CreateScope();
        await AddClientAsync(
            scope.ServiceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(settings),
            []
        );
    }

    private async Task AddClientAsync(
        IServiceProvider serviceProvider,
        string clientId,
        System.Text.Json.JsonElement settingsJson,
        IReadOnlyList<PersistedSecret> secrets
    )
    {
        var storeManagerFactory = serviceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<IClientStore>();

        var persistedClient = new PersistedClient
        {
            TenantId = TenantId,
            ClientId = clientId,
            IsDisabled = false,
            Settings = new PersistedClientSettings
            {
                TenantId = TenantId,
                ClientId = clientId,
                Value = settingsJson,
            },
            Secrets = new PersistedClientSecrets
            {
                TenantId = TenantId,
                ClientId = clientId,
                Value = [.. secrets],
            },
        };

        await store.AddAsync(persistedClient, CancellationToken.None);
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Ensures the tenant exists and seeds a confidential <c>client_secret_post</c> client that selects the
    /// <paramref name="securityProfiles"/> (for example <c>fapi2</c>), so profile-driven setting ceilings and
    /// enforcement can be exercised end-to-end. An empty list is stored as an explicit empty selection.
    /// </summary>
    public async Task SeedConfidentialClientWithSecurityProfilesAsync(
        string clientId,
        string clientSecret,
        IReadOnlyCollection<string> securityProfiles,
        params string[] grantTypes
    )
    {
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var protector = await serviceProvider
            .GetRequiredService<ISecretProtectorProvider>()
            .GetProtectorAsync(SecretOwner.ForTenant(TenantId), [], CancellationToken.None);

        var secretId = $"{clientId}-secret";
        var secretBytes = SecureEncoding.UTF8.GetBytes(clientSecret);

        var persistedSecret = new PersistedSecret
        {
            SecretId = secretId,
            Use = null,
            Algorithm = null,
            CreatedWhen = DateTimeOffset.UnixEpoch,
            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(100),
            SecretType = SecretTypes.Symmetric,
            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.SymmetricRaw,
            KeySizeBits = secretBytes.Length * 8,
            EncodedValue = protector.ProtectToString(
                secretBytes,
                SecretProtectionContext.ForPersistedSecret(secretId)
            ),
        };

        var settings = new Dictionary<string, object>
        {
            ["token_endpoint_auth_method"] = OpenIdProtocol
                .ClientAuthenticationMethods
                .ClientSecretPost,
            ["security_profiles"] = securityProfiles,
        };
        if (grantTypes.Length > 0)
            settings["grant_types_supported"] = grantTypes;

        await AddClientAsync(
            serviceProvider,
            clientId,
            System.Text.Json.JsonSerializer.SerializeToElement(settings),
            [persistedSecret]
        );
    }

    /// <summary>
    /// Seeds a resource server owning the given scopes and a client grant authorizing the client to all of them,
    /// so the client may request those scopes (replaces the retired <c>scopes_supported</c> setting).
    /// </summary>
    public Task SeedResourceServerWithClientGrantAsync(
        string clientId,
        string resourceServerId,
        string identifier,
        params string[] scopes
    ) =>
        SeedResourceServerWithClientGrantAsync(
            clientId,
            resourceServerId,
            identifier,
            new Dictionary<string, object>(),
            scopes
        );

    /// <summary>
    /// Seeds a resource server carrying the given settings document and owning the given scopes, and a client grant
    /// authorizing the client to all of them. The settings bound every access token issued for the resource server.
    /// </summary>
    public async Task SeedResourceServerWithClientGrantAsync(
        string clientId,
        string resourceServerId,
        string identifier,
        IReadOnlyDictionary<string, object> settings,
        params string[] scopes
    )
    {
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        var settingsJson = System.Text.Json.JsonSerializer.SerializeToElement(settings);

        using var scope = Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var resourceServerStore = storeManager.GetStore<IResourceServerStore>();
        var clientGrantStore = storeManager.GetStore<IClientGrantStore>();

        await resourceServerStore.AddAsync(
            new PersistedResourceServer
            {
                TenantId = TenantId,
                ResourceServerId = resourceServerId,
                Identifier = identifier,
                ConcurrencyToken = string.Empty,
                Name = resourceServerId,
                IsSystem = false,
                IsDisabled = false,
                Settings = new PersistedResourceServerSettings
                {
                    TenantId = TenantId,
                    ResourceServerId = resourceServerId,
                    Value = settingsJson,
                },
                Scopes = scopes
                    .Select(value => new PersistedScope
                    {
                        Value = value,
                        Description = null,
                        IsSystem = false,
                    })
                    .ToList(),
                Secrets = new PersistedResourceServerSecrets
                {
                    TenantId = TenantId,
                    ResourceServerId = resourceServerId,
                    Value = [],
                },
            },
            CancellationToken.None
        );
        // Persist the resource server before the grant: the grant store resolves the resource-server foreign key,
        // and an in-memory query does not observe unsaved inserts.
        await storeManager.SaveChangesAsync(CancellationToken.None);

        await clientGrantStore.AddAsync(
            new PersistedClientGrant
            {
                TenantId = TenantId,
                ClientId = clientId,
                ResourceServerId = resourceServerId,
                ConcurrencyToken = string.Empty,
                Scopes = scopes,
            },
            CancellationToken.None
        );
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Directly persists the tenant and a confidential client whose stored secret is protected from
    /// <paramref name="storedSecret"/>, without an HTTP warm-up that would let a configured bootstrap seeder create
    /// the client first. Use this to stage a pre-existing client (for example with a stale secret) that a later
    /// request's provisioning pass must reconcile. The client registers no client-authentication method, so it uses the
    /// default, <c>client_secret_basic</c>.
    /// </summary>
    public async Task SeedClientDirectlyAsync(string clientId, string storedSecret)
    {
        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var storeManagerFactory = serviceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );

        var tenantStore = storeManager.GetStore<ITenantStore>();
        if (await tenantStore.GetOrDefaultAsync(TenantId, CancellationToken.None) is null)
        {
            await tenantStore.AddAsync(CreateEmptyTenant(TenantId), CancellationToken.None);
            await storeManager.SaveChangesAsync(CancellationToken.None);
        }

        var protector = await serviceProvider
            .GetRequiredService<ISecretProtectorProvider>()
            .GetProtectorAsync(SecretOwner.ForTenant(TenantId), [], CancellationToken.None);
        var secretId = $"{clientId}-secret";
        var secretBytes = SecureEncoding.UTF8.GetBytes(storedSecret);

        var emptySettingsJson = System.Text.Json.JsonSerializer.SerializeToElement(
            new Dictionary<string, object>()
        );

        var clientStore = storeManager.GetStore<IClientStore>();
        await clientStore.AddAsync(
            new PersistedClient
            {
                TenantId = TenantId,
                ClientId = clientId,
                IsDisabled = false,
                Settings = new PersistedClientSettings
                {
                    TenantId = TenantId,
                    ClientId = clientId,
                    Value = emptySettingsJson,
                },
                Secrets = new PersistedClientSecrets
                {
                    TenantId = TenantId,
                    ClientId = clientId,
                    Value =
                    [
                        new PersistedSecret
                        {
                            SecretId = secretId,
                            Use = null,
                            Algorithm = null,
                            CreatedWhen = DateTimeOffset.UnixEpoch,
                            ExpiresWhen = DateTimeOffset.UnixEpoch.AddYears(100),
                            SecretType = SecretTypes.Symmetric,
                            SecretEncoding = NCode.Identity.Secrets.SecretEncodings.SymmetricRaw,
                            KeySizeBits = secretBytes.Length * 8,
                            EncodedValue = protector.ProtectToString(
                                secretBytes,
                                SecretProtectionContext.ForPersistedSecret(secretId)
                            ),
                        },
                    ],
                },
            },
            CancellationToken.None
        );
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Ensures the tenant exists and seeds an enabled local account whose stored password hash matches
    /// <paramref name="password"/>, so the resource-owner <c>password</c> grant can be exercised. Returns the
    /// generated <c>LocalAccountId</c> (the subject of the account's self-issued federated identity).
    /// </summary>
    public async Task<string> SeedLocalAccountAsync(
        string userName,
        string password,
        string? email = null,
        bool emailVerified = false
    )
    {
        // A first request forces the static tenant provider to persist the tenant entity,
        // which LocalAccountStore.AddAsync requires as a foreign key.
        using (var warmupClient = CreateClient())
        {
            using var _ = await warmupClient.GetAsync("/oauth2/jwks");
        }

        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var localAccountId = Guid.NewGuid().ToString("N");

        var storeManagerFactory = serviceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
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
            CancellationToken.None
        );
        await storeManager.SaveChangesAsync(CancellationToken.None);

        return localAccountId;
    }

    /// <summary>
    /// Persists the tenant with the given settings and no keys, so the tenant's keys are the ones the host provisions
    /// when it first seeds the tenant (<c>OpenId:Tenant:KeyProvisioning</c>, under <c>DeveloperKeys:Mode</c>
    /// <c>None</c>). The tenant reads its settings and keys when it is first served, so this must be called before the
    /// factory serves any request.
    /// </summary>
    /// <param name="settings">The tenant's settings document.</param>
    public async Task SeedTenantSettingsAsync(IReadOnlyDictionary<string, object> settings)
    {
        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var storeManagerFactory = serviceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var tenantStore = storeManager.GetStore<ITenantStore>();
        if (await tenantStore.GetOrDefaultAsync(TenantId, CancellationToken.None) is not null)
        {
            throw new InvalidOperationException(
                "The tenant is already persisted, so it may already be served with its earlier settings."
            );
        }

        var tenant = CreateEmptyTenant(TenantId);
        tenant.Settings.Value = System.Text.Json.JsonSerializer.SerializeToElement(settings);
        await tenantStore.AddAsync(tenant, CancellationToken.None);
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Replaces the settings document that the store holds for the tenant. It writes through the store, without the
    /// management API's checks and without evicting the tenant that the host materialized, so a management request
    /// reads a document the settings vocabulary may not honor while the caller's token still validates.
    /// </summary>
    /// <param name="settings">The tenant's settings document.</param>
    public async Task ReplaceTenantSettingsAsync(IReadOnlyDictionary<string, object> settings)
    {
        using var scope = Services.CreateScope();
        var serviceProvider = scope.ServiceProvider;

        var storeManagerFactory = serviceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var tenantStore = storeManager.GetStore<ITenantStore>();
        var tenantSettings =
            await tenantStore.GetSettingsOrDefaultAsync(TenantId, CancellationToken.None)
            ?? throw new InvalidOperationException(
                "The tenant is not persisted yet; serve a request first."
            );

        tenantSettings.Value = System.Text.Json.JsonSerializer.SerializeToElement(settings);
        await tenantStore.UpdateSettingsAsync(tenantSettings, CancellationToken.None);
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    /// <summary>
    /// Creates an HTTP client that calls the management API as the given client: it obtains a
    /// <c>client_credentials</c> access token with the <c>read:clients</c> scope, then sends it as a bearer token.
    /// </summary>
    /// <param name="clientId">The identifier of the client that authenticates with a shared secret.</param>
    /// <param name="clientSecret">The client's secret, as sent to the token endpoint.</param>
    /// <returns>The HTTP client, which does not follow redirects and carries the bearer token.</returns>
    public async Task<HttpClient> CreateManagementClientAsync(string clientId, string clientSecret)
    {
        const string managementScope = "read:clients";

        var client = CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        var credentials = SecureEncoding.UTF8.GetBytes($"{clientId}:{clientSecret}");
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(credentials)
                ),
            },
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                    ["scope"] = managementScope,
                }
            ),
        };

        using var response = await client.SendAsync(tokenRequest);
        response.EnsureSuccessStatusCode();
        using var document = System.Text.Json.JsonDocument.Parse(
            await response.Content.ReadAsStringAsync()
        );
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            document.RootElement.GetProperty("access_token").GetString()
        );
        return client;
    }

    private PersistedTenant CreateEmptyTenant(string tenantId) =>
        new()
        {
            TenantId = tenantId,
            Issuer = TestServerIssuer,
            ConcurrencyToken = Guid.NewGuid().ToString("N"),
            IsDisabled = false,
            DisplayName = tenantId,
            Settings = new PersistedTenantSettings
            {
                TenantId = tenantId,
                ConcurrencyToken = Guid.NewGuid().ToString("N"),
                Value = System.Text.Json.JsonSerializer.SerializeToElement(null, typeof(object)),
            },
            Secrets = new PersistedTenantSecrets
            {
                TenantId = tenantId,
                ConcurrencyToken = Guid.NewGuid().ToString("N"),
                Value = [],
            },
        };

    /// <inheritdoc />
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Resolve the content root to the host project so its appsettings.json is loaded.
        builder.UseSolutionRelativeContentRoot("src/NCode.Identity.OpenId.Host");
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

            // Widen the server ceiling (registered after the library default, so it unions on top):
            // enables the client_credentials and resource-owner password grants plus a custom "api" scope for
            // integration coverage. Both grants stay off in the host's own secure defaults.
            serviceCollection.AddSingleton<IDefaultSettingsProvider, TestServerSettingsProvider>();

            // Seed no developer clients unless a derived factory opts in (the last registration wins, so the
            // developer seeder sees an empty set of clients).
            if (!SeedsDeveloperClients)
            {
                serviceCollection.AddSingleton(new DeveloperInteractiveSeedOptions());
            }

            // Replace the SSRF-guarded client jwks_uri HTTP client with a stub serving canned JSON, so a jwks_uri
            // fetch can be exercised without a real outbound connection (which the guard would refuse).
            if (ClientJwksResponder is { } responder)
            {
                serviceCollection
                    .AddHttpClient("NCode.Identity.OpenId.ClientJwks")
                    .ConfigurePrimaryHttpMessageHandler(() =>
                        new StubJsonHttpMessageHandler(responder)
                    );
            }

            // Replace the SSRF-guarded certificate revocation HTTP client with a stub serving a canned CRL, so a CRL
            // fetch can be exercised without a real outbound connection (which the guard would refuse).
            if (CertificateRevocationListResponder is { } crlResponder)
            {
                serviceCollection
                    .AddHttpClient("NCode.Identity.OpenId.ClientCertificateRevocation")
                    .ConfigurePrimaryHttpMessageHandler(() =>
                        new StubBinaryHttpMessageHandler(crlResponder)
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
