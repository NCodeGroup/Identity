// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.RemoteKeys;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Drives a host configured the way production runs it, no developer keys, whose tenants keep their signing keys in a
/// key management service: the keys are stored as references to the service, the key set publishes their stored public
/// halves, the service signs each token, a tenant that opts in later hands its keys over after the activation delay,
/// and deleting a remote key destroys it in the service.
/// </summary>
public class RemoteSigningKeyTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string RootIssuer = "http://localhost";
    private const string IssuerTemplate = "https://{tenantId}.auth.test";
    private const string JwksPath = "/oauth2/jwks";
    private const string ProviderSettingName = OpenIdSettingNames.TenantSigningKeyProvider;
    private const string ProviderSettingConfigKey =
        $"OpenId:Platform:Settings:{ProviderSettingName}";

    private static string BootstrapClientSecretConfig { get; } =
        Base64Url.EncodeToString(SecureEncoding.UTF8.GetBytes(BootstrapClientSecret));

    private static Dictionary<string, string?> CreateStaticConfiguration() =>
        new()
        {
            ["BootstrapAdmin:ClientId"] = BootstrapClientId,
            ["BootstrapAdmin:ClientSecret"] = BootstrapClientSecretConfig,
            ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
            [ProviderSettingConfigKey] = TestRemoteKeyProvider.ProviderName,
        };

    private static Dictionary<string, string?> CreateDynamicConfiguration() =>
        new()
        {
            ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                .TenantStrategyCodes
                .DynamicByHost,
            ["OpenId:TenantResolution:Root:Issuer"] = RootIssuer,
            ["OpenId:TenantResolution:DynamicByHost:IssuerTemplate"] = IssuerTemplate,
            ["BootstrapAdmin:ClientId"] = BootstrapClientId,
            ["BootstrapAdmin:ClientSecret"] = BootstrapClientSecretConfig,
            ["BootstrapAdmin:TenantId"] = "root",
        };

    #region Provisioning Tests

    [Fact]
    public async Task GetJwks_WhenTheTenantSelectsAProvider_StoresEachProvisionedKeyAsRemote()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteSigningApplicationFactory(
            provider,
            CreateStaticConfiguration()
        );
        using var client = factory.CreateClient();

        // The first request serves the tenant, which provisions its keys.
        await GetJwksAsync(client, host: null);

        var secrets = await GetStoredSecretsAsync(factory, HostApplicationFactory.TenantId);
        Assert.Equal(3, secrets.Count);
        Assert.Equal(
            ["ES256", "PS256", "RS256"],
            secrets.Select(secret => secret.Algorithm).Order()
        );
        Assert.All(
            secrets,
            secret =>
            {
                Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
                Assert.Equal(SecretOrigins.Generated, secret.Origin);
                Assert.False(string.IsNullOrEmpty(secret.PublicKey));
                Assert.True(RemoteKeyReference.TryParse(secret.EncodedValue, out var reference));
                Assert.Equal(TestRemoteKeyProvider.ProviderName, reference.Provider);
            }
        );

        // A seed that loses its commit generates again from what the winner wrote, so the provider may hold keys that no
        // secret references; every stored reference names a key that the provider created.
        var created = provider.CreatedKeyIds;
        Assert.All(secrets, secret => Assert.Contains(ParseKeyId(secret), created));
        Assert.Equal(3, secrets.Select(secret => ParseKeyId(secret)).Distinct().Count());
    }

    #endregion

    #region Key Set Tests

    [Fact]
    public async Task GetJwks_WhenTheKeysAreRemote_PublishesTheirStoredPublicKeysWithoutCallingTheProvider()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteSigningApplicationFactory(
            provider,
            CreateStaticConfiguration()
        );
        using var client = factory.CreateClient();

        var keys = await GetJwksAsync(client, host: null);

        var secrets = await GetStoredSecretsAsync(factory, HostApplicationFactory.TenantId);
        Assert.Equal(
            secrets.Select(secret => secret.SecretId).Order(StringComparer.Ordinal),
            keys.Select(key => key.GetProperty("kid").GetString() ?? string.Empty)
                .Order(StringComparer.Ordinal)
        );
        Assert.All(keys, key => Assert.Equal("sig", key.GetProperty("use").GetString()));

        var rs256 = Assert.Single(keys, key => key.GetProperty("alg").GetString() == "RS256");
        Assert.Equal(
            3072 / 8,
            Base64Url.DecodeFromChars(rs256.GetProperty("n").GetString()).Length
        );
        var es256 = Assert.Single(keys, key => key.GetProperty("alg").GetString() == "ES256");
        Assert.Equal("P-256", es256.GetProperty("crv").GetString());

        Assert.Equal(0, provider.SignatureCount);
    }

    #endregion

    #region Signing Tests

    [Fact]
    public async Task PostToken_WhenTheKeysAreRemote_IssuesATokenTheProviderSignedAndTheKeySetVerifies()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteSigningApplicationFactory(
            provider,
            CreateStaticConfiguration()
        );
        using var client = factory.CreateClient();

        var accessToken = await GetBootstrapTokenAsync(client);

        Assert.True(provider.SignatureCount > 0);
        var keys = await GetJwksAsync(client, host: null);
        Assert.True(VerifyJwt(accessToken, keys));
    }

    [Fact]
    public async Task PostToken_WhenTheSignatureIsAltered_DoesNotVerifyAgainstThePublishedKey()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteSigningApplicationFactory(
            provider,
            CreateStaticConfiguration()
        );
        using var client = factory.CreateClient();

        var accessToken = await GetBootstrapTokenAsync(client);
        var keys = await GetJwksAsync(client, host: null);
        var kid = DecodeJwtHeader(accessToken).GetProperty("kid").GetString();
        var tampered = $"{accessToken[..^4]}AAAA";

        // The control verifies and the altered token, which names the same published key, does not.
        Assert.Contains(keys, key => key.GetProperty("kid").GetString() == kid);
        Assert.True(VerifyJwt(accessToken, keys));
        Assert.False(VerifyJwt(tampered, keys));
    }

    #endregion

    #region Custody Change Tests

    [Fact]
    public async Task PatchPlatformSettings_WhenATenantStartsLocal_PublishesRemoteKeysThatActivateAfterTheDelayWhileLocalKeysSign()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteSigningApplicationFactory(
            provider,
            CreateDynamicConfiguration()
        );
        using var client = factory.CreateClient();

        var accessToken = await GetBootstrapTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );
        await CreateTenantAsync(client, "acme");

        var local = await GetStoredSecretsAsync(factory, "acme");
        Assert.Equal(3, local.Count);
        Assert.All(local, secret => Assert.NotEqual(SecretEncodings.Remote, secret.SecretEncoding));
        Assert.Empty(provider.CreatedKeyIds);

        await PatchPlatformSettingAsync(client);

        // The platform's change reconciles every team in the background, so the remote keys arrive after it returns.
        var secrets = await WaitForSecretsAsync(factory, "acme", count: 6);
        var remote = secrets
            .Where(secret => secret.SecretEncoding == SecretEncodings.Remote)
            .ToList();
        Assert.Equal(3, remote.Count);
        Assert.Equal(
            ["ES256", "PS256", "RS256"],
            remote.Select(secret => secret.Algorithm).Order()
        );

        foreach (var remoteSecret in remote)
        {
            var superseded = Assert.Single(
                local,
                secret => secret.Algorithm == remoteSecret.Algorithm
            );
            Assert.True(remoteSecret.ActivatesWhen > remoteSecret.CreatedWhen);
            Assert.Equal(
                remoteSecret.ActivatesWhen,
                secrets.Single(secret => secret.SecretId == superseded.SecretId).RetiresWhen
            );
        }

        // The key set publishes the remote keys ahead of their activation beside the local keys.
        var keys = await GetJwksAsync(client, host: "acme.auth.test");
        Assert.Equal(
            secrets.Select(secret => secret.SecretId).Order(StringComparer.Ordinal),
            keys.Select(key => key.GetProperty("kid").GetString() ?? string.Empty)
                .Order(StringComparer.Ordinal)
        );

        // The local keys still sign: the root tenant's token carries a local key and the provider signed nothing.
        var rootToken = await GetBootstrapTokenAsync(client);
        var rootSecrets = await GetStoredSecretsAsync(factory, "root");
        var signingKey = Assert.Single(
            rootSecrets,
            secret => secret.SecretId == DecodeJwtHeader(rootToken).GetProperty("kid").GetString()
        );
        Assert.NotEqual(SecretEncodings.Remote, signingKey.SecretEncoding);
        Assert.Equal(0, provider.SignatureCount);
    }

    #endregion

    #region Destruction Tests

    [Fact]
    public async Task DeleteSecret_WhenTheSecretIsRemote_DestroysItsKeyInTheProvider()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteSigningApplicationFactory(
            provider,
            CreateStaticConfiguration()
        );
        using var client = factory.CreateClient();
        var accessToken = await GetBootstrapTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );

        var secrets = await GetStoredSecretsAsync(factory, HostApplicationFactory.TenantId);
        var es256 = Assert.Single(secrets, secret => secret.Algorithm == "ES256");
        Assert.Equal(SecretEncodings.Remote, es256.SecretEncoding);
        Assert.Empty(provider.DestroyedKeyIds);

        using var response = await client.DeleteAsync(
            $"/api/tenants/{HostApplicationFactory.TenantId}/secrets/{es256.SecretId}"
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, body);

        Assert.Equal([ParseKeyId(es256)], provider.DestroyedKeyIds);
    }

    #endregion

    #region Management Tests

    [Fact]
    public async Task PostRemoteSecret_WhenTheProviderIsRegistered_CreatesTheKeyInTheProviderAndNamesIt()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteSigningApplicationFactory(
            provider,
            CreateStaticConfiguration()
        );
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var expiresWhen = GetExpiresWhen(factory);

        using var response = await client.PostAsJsonAsync(
            $"/api/tenants/{HostApplicationFactory.TenantId}/secrets/remote",
            new
            {
                remoteKeyProvider = TestRemoteKeyProvider.ProviderName,
                secretType = "ecc",
                keySizeBits = 256,
                algorithm = "ES256",
                expiresWhen,
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);

        var resource = JsonElement.Parse(body);
        Assert.Equal(
            TestRemoteKeyProvider.ProviderName,
            resource.GetProperty("remoteKeyProvider").GetString()
        );

        var secretId = resource.GetProperty("secretId").GetString();
        var secrets = await GetStoredSecretsAsync(factory, HostApplicationFactory.TenantId);
        var secret = Assert.Single(secrets, candidate => candidate.SecretId == secretId);
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal(SecretOrigins.Generated, secret.Origin);
        Assert.Null(secret.ManagedBy);
        Assert.Contains(ParseKeyId(secret), provider.CreatedKeyIds);
    }

    [Fact]
    public async Task PostRemoteSecretImport_WhenTheKeySignsWithTheSuppliedPublicKey_BindsIt()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteSigningApplicationFactory(
            provider,
            CreateStaticConfiguration()
        );
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);

        var secretId = await BindKeyAsync(factory, client, provider);

        var secrets = await GetStoredSecretsAsync(factory, HostApplicationFactory.TenantId);
        var secret = Assert.Single(secrets, candidate => candidate.SecretId == secretId);
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal(SecretOrigins.Supplied, secret.Origin);
        Assert.True(provider.SignatureCount > 0);
    }

    [Fact]
    public async Task PostRemoteSecretImport_WhenThePublicKeyIsAnotherKeys_ReturnsBadRequest()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteSigningApplicationFactory(
            provider,
            CreateStaticConfiguration()
        );
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var before = await GetStoredSecretsAsync(factory, HostApplicationFactory.TenantId);

        using var held = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var other = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyId = provider.AddKey(held);
        var expiresWhen = GetExpiresWhen(factory);
        var importBody = CreateImportBody(
            keyId,
            other.ExportSubjectPublicKeyInfoPem(),
            expiresWhen
        );

        using var response = await PostImportAsync(client, importBody);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);

        var after = await GetStoredSecretsAsync(factory, HostApplicationFactory.TenantId);
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public async Task DeleteSecret_WhenTheKeyWasBound_LeavesItInTheProvider()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteSigningApplicationFactory(
            provider,
            CreateStaticConfiguration()
        );
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);
        var secretId = await BindKeyAsync(factory, client, provider);

        using var response = await client.DeleteAsync(
            $"/api/tenants/{HostApplicationFactory.TenantId}/secrets/{secretId}"
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, body);

        Assert.Empty(provider.DestroyedKeyIds);
    }

    #endregion

    private static async Task AuthorizeAsync(HttpClient client)
    {
        var accessToken = await GetBootstrapTokenAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessToken
        );
    }

    private static DateTimeOffset GetExpiresWhen(HostApplicationFactory factory) =>
        factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().AddYears(1);

    private static async Task<string> BindKeyAsync(
        HostApplicationFactory factory,
        HttpClient client,
        TestRemoteKeyProvider provider
    )
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyId = provider.AddKey(
            ECDsa.Create(key.ExportParameters(includePrivateParameters: true))
        );
        var expiresWhen = GetExpiresWhen(factory);
        var importBody = CreateImportBody(keyId, key.ExportSubjectPublicKeyInfoPem(), expiresWhen);

        using var response = await PostImportAsync(client, importBody);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);

        return JsonElement.Parse(body).GetProperty("secretId").GetString() ?? string.Empty;
    }

    private static object CreateImportBody(
        string keyId,
        string publicKeyPem,
        DateTimeOffset expiresWhen
    ) =>
        new
        {
            remoteKeyProvider = TestRemoteKeyProvider.ProviderName,
            remoteKeyId = keyId,
            secretType = "ecc",
            format = "pem",
            keyMaterial = publicKeyPem,
            algorithm = "ES256",
            expiresWhen,
        };

    private static Task<HttpResponseMessage> PostImportAsync(HttpClient client, object body) =>
        client.PostAsJsonAsync(
            $"/api/tenants/{HostApplicationFactory.TenantId}/secrets/remote/import",
            body
        );

    private static string ParseKeyId(PersistedSecret secret)
    {
        Assert.True(RemoteKeyReference.TryParse(secret.EncodedValue, out var reference));
        return reference.KeyId;
    }

    private static async Task CreateTenantAsync(HttpClient client, string tenantId)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/teams/{HostApplicationFactory.TeamId}/tenants",
            new
            {
                tenantId,
                displayName = tenantId,
                isDisabled = false,
                settings = new { },
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    private static async Task PatchPlatformSettingAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/platform/settings")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new[]
                    {
                        new
                        {
                            op = "add",
                            path = $"/{ProviderSettingName}",
                            value = TestRemoteKeyProvider.ProviderName,
                        },
                    }
                ),
                SecureEncoding.UTF8,
                "application/json-patch+json"
            ),
        };

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, body);
    }

    private static async Task<List<JsonElement>> GetJwksAsync(HttpClient client, string? host)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, JwksPath);
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        return [.. jsonElement.GetProperty("keys").EnumerateArray().Select(key => key.Clone())];
    }

    // The bootstrap client registers client_secret_basic, so it presents its secret with HTTP Basic authentication.
    private static async Task<string> GetBootstrapTokenAsync(HttpClient client)
    {
        var credentials = SecureEncoding.UTF8.GetBytes(
            $"{BootstrapClientId}:{BootstrapClientSecret}"
        );
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
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
                    ["scope"] = "read:clients",
                }
            ),
        };

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        return jsonElement.GetProperty("access_token").GetString() ?? string.Empty;
    }

    private static JsonElement DecodeJwtHeader(string jwt) =>
        JsonElement.Parse(Base64Url.DecodeFromChars(jwt.Split('.')[0]));

    // Verifies the JWS signature with the published key that the header's kid names. It verifies with the published
    // JWK through the platform's primitives, independent of the JOSE stack, so a shared misreading in the stack cannot
    // pass.
    private static bool VerifyJwt(string jwt, IReadOnlyList<JsonElement> keys)
    {
        var parts = jwt.Split('.');
        var header = DecodeJwtHeader(jwt);
        var kid = header.GetProperty("kid").GetString();
        var algorithm = header.GetProperty("alg").GetString();
        var key = Assert.Single(keys, key => key.GetProperty("kid").GetString() == kid);

        var signingInput = SecureEncoding.ASCII.GetBytes($"{parts[0]}.{parts[1]}");
        var signature = Base64Url.DecodeFromChars(parts[2]);

        switch (algorithm)
        {
            case "RS256":
            case "PS256":
                using (var rsa = RSA.Create())
                {
                    rsa.ImportParameters(
                        new RSAParameters
                        {
                            Modulus = Base64Url.DecodeFromChars(key.GetProperty("n").GetString()),
                            Exponent = Base64Url.DecodeFromChars(key.GetProperty("e").GetString()),
                        }
                    );
                    return rsa.VerifyData(
                        signingInput,
                        signature,
                        HashAlgorithmName.SHA256,
                        algorithm == "PS256" ? RSASignaturePadding.Pss : RSASignaturePadding.Pkcs1
                    );
                }

            case "ES256":
                using (var ecdsa = ECDsa.Create())
                {
                    ecdsa.ImportParameters(
                        new ECParameters
                        {
                            Curve = ECCurve.NamedCurves.nistP256,
                            Q = new ECPoint
                            {
                                X = Base64Url.DecodeFromChars(key.GetProperty("x").GetString()),
                                Y = Base64Url.DecodeFromChars(key.GetProperty("y").GetString()),
                            },
                        }
                    );
                    return ecdsa.VerifyData(signingInput, signature, HashAlgorithmName.SHA256);
                }

            default:
                throw new InvalidOperationException($"The test does not verify '{algorithm}'.");
        }
    }

    // A change of the platform's settings reconciles every team in the background, so the keys of the tenants
    // arrive after the change that asks for them returns.
    private static async Task<List<PersistedSecret>> WaitForSecretsAsync(
        HostApplicationFactory factory,
        string tenantId,
        int count
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var secrets = await GetStoredSecretsAsync(factory, tenantId);
        while (secrets.Count < count)
        {
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(15),
                $"The tenant '{tenantId}' holds {secrets.Count} of {count} secrets after 15 seconds."
            );
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            secrets = await GetStoredSecretsAsync(factory, tenantId);
        }

        return secrets;
    }

    private static async Task<List<PersistedSecret>> GetStoredSecretsAsync(
        HostApplicationFactory factory,
        string tenantId
    )
    {
        using var serviceScope = factory.Services.CreateScope();
        await using var storeManager = await serviceScope
            .ServiceProvider.GetRequiredService<IStoreManagerFactory>()
            .CreateAsync(CancellationToken.None);
        var secrets = await storeManager
            .GetStore<ITenantStore>()
            .GetSecretsAsync(tenantId, CancellationToken.None);
        return [.. secrets.Value];
    }

    /// <summary>
    /// A factory with the supplied configuration overlaid and <see cref="TestRemoteKeyProvider"/> registered.
    /// </summary>
    private sealed class RemoteSigningApplicationFactory(
        TestRemoteKeyProvider provider,
        IReadOnlyDictionary<string, string?> configuration
    ) : HostApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration(configurationBuilder =>
                configurationBuilder.AddInMemoryCollection(configuration)
            );
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IRemoteKeyProvider>(provider);
                services.AddSingleton<IRemoteKeyDescriptorSource>(provider);
            });
        }
    }
}
