// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Drives a host whose tenants keep their encryption keys in a key management service: the key is stored as a
/// reference to the service, the key set publishes its stored public half, the service unwraps the content key of each
/// JWE sent to the tenant, and binding a key the service holds first proves that it decrypts.
/// </summary>
public class RemoteEncryptionKeyTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string JwksPath = "/oauth2/jwks";
    private const string ProviderSettingConfigKey =
        $"OpenId:Platform:Settings:{OpenIdSettingNames.TenantKeyProvider}";

    private static string BootstrapClientSecretConfig { get; } =
        Base64Url.EncodeToString(SecureEncoding.UTF8.GetBytes(BootstrapClientSecret));

    // The test provider's RSA rows are 3072-bit, so the provisioned keys name that size.
    private static Dictionary<string, string?> CreateStaticConfiguration() =>
        new()
        {
            ["BootstrapAdmin:ClientId"] = BootstrapClientId,
            ["BootstrapAdmin:ClientSecret"] = BootstrapClientSecretConfig,
            ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
            [ProviderSettingConfigKey] = TestRemoteKeyProvider.ProviderName,
            ["OpenId:Tenant:KeyProvisioning:Keys:0:Algorithm"] = "RS256",
            ["OpenId:Tenant:KeyProvisioning:Keys:0:KeySizeBits"] = "3072",
            ["OpenId:Tenant:KeyProvisioning:Keys:1:Algorithm"] = "RSA-OAEP-256",
            ["OpenId:Tenant:KeyProvisioning:Keys:1:KeySizeBits"] = "3072",
        };

    #region Provisioning Tests

    [Fact]
    public async Task GetJwks_WhenTheTenantSelectsAProvider_StoresTheEncryptionKeyAsRemote()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();

        // The first request serves the tenant, which provisions its keys.
        var keys = await GetJwksAsync(client);

        var secrets = await GetStoredSecretsAsync(factory, HostApplicationFactory.TenantId);
        var secret = Assert.Single(secrets, candidate => candidate.Algorithm == "RSA-OAEP-256");
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal("enc", secret.Use);

        var key = Assert.Single(
            keys,
            candidate => candidate.GetProperty("use").GetString() == "enc"
        );
        Assert.Equal("RSA-OAEP-256", key.GetProperty("alg").GetString());
    }

    #endregion

    #region Decryption Tests

    [Fact]
    public async Task DecodeAsync_ToTheTenantsRemoteEncryptionKey_DecryptsThroughTheProvider()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();
        await GetJwksAsync(client);

        var secrets = await GetStoredSecretsAsync(factory, HostApplicationFactory.TenantId);
        var persistedSecret = Assert.Single(
            secrets,
            candidate => candidate.Algorithm == "RSA-OAEP-256"
        );

        var secretSerializer = factory.Services.GetRequiredService<ISecretSerializer>();
        var joseSerializer = factory.Services.GetRequiredService<IJoseSerializer>();
        var algorithmCollection = factory
            .Services.GetRequiredService<IAlgorithmCollectionProvider>()
            .Collection;
        Assert.True(
            algorithmCollection.TryGetKeyManagementAlgorithm(
                AlgorithmCodes.KeyManagement.RsaOaep256,
                out var keyManagementAlgorithm
            )
        );
        Assert.True(
            algorithmCollection.TryGetAuthenticatedEncryptionAlgorithm(
                AlgorithmCodes.AuthenticatedEncryption.Aes256Gcm,
                out var encryptionAlgorithm
            )
        );

        var key = secretSerializer.DeserializeRemoteSecret(persistedSecret);
        const string payload = """{"sub":"alice"}""";
        var token = await joseSerializer
            .CreateEncoder(
                new JoseEncryptionOptions(
                    new JoseEncryptionCredentials(
                        key,
                        keyManagementAlgorithm,
                        encryptionAlgorithm,
                        null
                    )
                )
            )
            .EncodeAsync(payload, CancellationToken.None);

        var decoded = await joseSerializer.DecodeAsync(token, key, CancellationToken.None);

        Assert.Equal(payload, decoded);
        Assert.Equal(1, provider.DecryptionCount);
    }

    #endregion

    #region Management Tests

    [Fact]
    public async Task PostRemoteSecretImport_WhenTheProviderHoldsADecryptionKey_BindsItAfterItDecrypts()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();
        await AuthorizeAsync(client);

        using var key = RSA.Create(3072);
        var keyId = provider.AddKey(
            RSA.Create(key.ExportParameters(includePrivateParameters: true))
        );
        var expiresWhen = factory
            .Services.GetRequiredService<TimeProvider>()
            .GetUtcNow()
            .AddYears(1);

        using var response = await client.PostAsJsonAsync(
            $"/api/tenants/{HostApplicationFactory.TenantId}/secrets/remote/import",
            new
            {
                remoteKeyProvider = TestRemoteKeyProvider.ProviderName,
                remoteKeyId = keyId,
                secretType = "rsa",
                keySizeBits = 3072,
                algorithm = "RSA-OAEP-256",
                expiresWhen,
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var secretId = JsonElement.Parse(body).GetProperty("secretId").GetString();

        var secrets = await GetStoredSecretsAsync(factory, HostApplicationFactory.TenantId);
        var secret = Assert.Single(secrets, candidate => candidate.SecretId == secretId);
        Assert.Equal("enc", secret.Use);
        Assert.Equal(SecretOrigins.Supplied, secret.Origin);
        Assert.Equal(Base64Url.EncodeToString(key.ExportSubjectPublicKeyInfo()), secret.PublicKey);
        Assert.True(provider.DecryptionCount > 0);
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

    private static async Task<List<JsonElement>> GetJwksAsync(HttpClient client)
    {
        using var response = await client.GetAsync(JwksPath);
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
}
