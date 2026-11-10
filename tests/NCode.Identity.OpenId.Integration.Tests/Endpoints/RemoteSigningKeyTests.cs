// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
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
    private const string RootIssuer = "http://localhost";
    private const string IssuerTemplate = "https://{tenantId}.auth.test";
    private const string JwksPath = "/oauth2/jwks";
    private const string ProviderSettingName = OpenIdSettingNames.TenantKeyProvider;
    private const string ProviderSettingConfigKey =
        $"OpenId:Platform:Settings:{ProviderSettingName}";

    private static Dictionary<string, string?> CreateStaticConfiguration() =>
        new() { [ProviderSettingConfigKey] = TestRemoteKeyProvider.ProviderName };

    private static Dictionary<string, string?> CreateDynamicConfiguration() =>
        new()
        {
            ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                .TenantStrategyCodes
                .DynamicByHost,
            ["OpenId:TenantResolution:Root:Issuer"] = RootIssuer,
            ["OpenId:TenantResolution:DynamicByHost:IssuerTemplate"] = IssuerTemplate,
        };

    #region Provisioning Tests

    [Fact]
    public async Task GetJwks_WhenTheTenantSelectsAProvider_StoresEachProvisionedKeyAsRemote()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();

        // The first request serves the tenant, which provisions its keys.
        await GetJwksAsync(client, host: null, CancellationToken.None);

        var secrets = await GetStoredSecretsAsync(
            factory,
            HostApplicationFactory.TenantId,
            CancellationToken.None
        );
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
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();

        var keys = await GetJwksAsync(client, host: null, CancellationToken.None);

        var secrets = await GetStoredSecretsAsync(
            factory,
            HostApplicationFactory.TenantId,
            CancellationToken.None
        );
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
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();

        var accessToken = await BootstrapAdminApplicationFactory.TokenRequest.GetAccessTokenAsync(
            client,
            CancellationToken.None
        );

        Assert.True(provider.SignatureCount > 0);
        var keys = await GetJwksAsync(client, host: null, CancellationToken.None);
        Assert.True(VerifyJwt(accessToken, keys));
    }

    [Fact]
    public async Task PostToken_WhenTheSignatureIsAltered_DoesNotVerifyAgainstThePublishedKey()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();

        var accessToken = await BootstrapAdminApplicationFactory.TokenRequest.GetAccessTokenAsync(
            client,
            CancellationToken.None
        );
        var keys = await GetJwksAsync(client, host: null, CancellationToken.None);
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
        using var factory = new RemoteKeyApplicationFactory(
            provider,
            CreateDynamicConfiguration(),
            "root"
        );
        using var client = factory.CreateClient();

        await BootstrapAdminApplicationFactory.TokenRequest.AuthorizeAsync(
            client,
            CancellationToken.None
        );
        await CreateTenantAsync(client, "acme", CancellationToken.None);

        var local = await GetStoredSecretsAsync(factory, "acme", CancellationToken.None);
        Assert.Equal(3, local.Count);
        Assert.All(local, secret => Assert.NotEqual(SecretEncodings.Remote, secret.SecretEncoding));
        Assert.Empty(provider.CreatedKeyIds);

        await PatchPlatformSettingAsync(client, CancellationToken.None);

        // The platform's change reconciles every team in the background, so the remote keys arrive after it returns.
        var secrets = await WaitForSecretsAsync(factory, "acme", count: 6, CancellationToken.None);
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
        var keys = await GetJwksAsync(client, host: "acme.auth.test", CancellationToken.None);
        Assert.Equal(
            secrets.Select(secret => secret.SecretId).Order(StringComparer.Ordinal),
            keys.Select(key => key.GetProperty("kid").GetString() ?? string.Empty)
                .Order(StringComparer.Ordinal)
        );

        // The local keys still sign: the root tenant's token carries a local key and the provider signed nothing.
        var rootToken = await BootstrapAdminApplicationFactory.TokenRequest.GetAccessTokenAsync(
            client,
            CancellationToken.None
        );
        var rootSecrets = await GetStoredSecretsAsync(factory, "root", CancellationToken.None);
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
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();
        await BootstrapAdminApplicationFactory.TokenRequest.AuthorizeAsync(
            client,
            CancellationToken.None
        );

        var secrets = await GetStoredSecretsAsync(
            factory,
            HostApplicationFactory.TenantId,
            CancellationToken.None
        );
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
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();
        await BootstrapAdminApplicationFactory.TokenRequest.AuthorizeAsync(
            client,
            CancellationToken.None
        );
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
        var secrets = await GetStoredSecretsAsync(
            factory,
            HostApplicationFactory.TenantId,
            CancellationToken.None
        );
        var secret = Assert.Single(secrets, candidate => candidate.SecretId == secretId);
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal(SecretOrigins.Generated, secret.Origin);
        Assert.Null(secret.ManagedBy);
        Assert.Contains(ParseKeyId(secret), provider.CreatedKeyIds);
    }

    [Fact]
    public async Task PostRemoteSecretImport_WhenTheProviderHoldsTheKey_BindsItWithThePublicKeyTheProviderReturns()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();
        await BootstrapAdminApplicationFactory.TokenRequest.AuthorizeAsync(
            client,
            CancellationToken.None
        );

        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var keyId = provider.AddKey(
            ECDsa.Create(key.ExportParameters(includePrivateParameters: true))
        );
        var importBody = CreateImportBody(keyId, GetExpiresWhen(factory));

        using var response = await PostImportAsync(client, importBody, CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var secretId = JsonElement.Parse(body).GetProperty("secretId").GetString();

        var secrets = await GetStoredSecretsAsync(
            factory,
            HostApplicationFactory.TenantId,
            CancellationToken.None
        );
        var secret = Assert.Single(secrets, candidate => candidate.SecretId == secretId);
        Assert.Equal(Base64Url.EncodeToString(key.ExportSubjectPublicKeyInfo()), secret.PublicKey);
        Assert.Equal(SecretEncodings.Remote, secret.SecretEncoding);
        Assert.Equal(SecretOrigins.Supplied, secret.Origin);
        Assert.True(provider.SignatureCount > 0);
    }

    [Fact]
    public async Task PostRemoteSecretImport_WhenTheKeyIsNotOfTheAlgorithmsKeyType_ReturnsBadRequest()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();
        await BootstrapAdminApplicationFactory.TokenRequest.AuthorizeAsync(
            client,
            CancellationToken.None
        );
        var before = await GetStoredSecretsAsync(
            factory,
            HostApplicationFactory.TenantId,
            CancellationToken.None
        );

        var keyId = provider.AddKey(RSA.Create(2048));
        var importBody = CreateImportBody(keyId, GetExpiresWhen(factory));

        using var response = await PostImportAsync(client, importBody, CancellationToken.None);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);

        var after = await GetStoredSecretsAsync(
            factory,
            HostApplicationFactory.TenantId,
            CancellationToken.None
        );
        Assert.Equal(before.Count, after.Count);
    }

    [Fact]
    public async Task DeleteSecret_WhenTheKeyWasBound_LeavesItInTheProvider()
    {
        using var provider = new TestRemoteKeyProvider();
        using var factory = new RemoteKeyApplicationFactory(provider, CreateStaticConfiguration());
        using var client = factory.CreateClient();
        await BootstrapAdminApplicationFactory.TokenRequest.AuthorizeAsync(
            client,
            CancellationToken.None
        );
        var secretId = await BindKeyAsync(factory, client, provider, CancellationToken.None);

        using var response = await client.DeleteAsync(
            $"/api/tenants/{HostApplicationFactory.TenantId}/secrets/{secretId}"
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, body);

        Assert.Empty(provider.DestroyedKeyIds);
    }

    #endregion

    private static DateTimeOffset GetExpiresWhen(HostApplicationFactory factory) =>
        factory.Services.GetRequiredService<TimeProvider>().GetUtcNow().AddYears(1);

    private static async ValueTask<string> BindKeyAsync(
        HostApplicationFactory factory,
        HttpClient client,
        TestRemoteKeyProvider provider,
        CancellationToken cancellationToken
    )
    {
        var keyId = provider.AddKey(ECDsa.Create(ECCurve.NamedCurves.nistP256));
        var importBody = CreateImportBody(keyId, GetExpiresWhen(factory));

        using var response = await PostImportAsync(client, importBody, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);

        return JsonElement.Parse(body).GetProperty("secretId").GetString() ?? string.Empty;
    }

    private static object CreateImportBody(string keyId, DateTimeOffset expiresWhen) =>
        new
        {
            remoteKeyProvider = TestRemoteKeyProvider.ProviderName,
            remoteKeyId = keyId,
            secretType = "ecc",
            keySizeBits = 256,
            curve = "P-256",
            algorithm = "ES256",
            expiresWhen,
        };

    private static async ValueTask<HttpResponseMessage> PostImportAsync(
        HttpClient client,
        object body,
        CancellationToken cancellationToken
    ) =>
        await client.PostAsJsonAsync(
            $"/api/tenants/{HostApplicationFactory.TenantId}/secrets/remote/import",
            body,
            cancellationToken
        );

    private static string ParseKeyId(PersistedSecret secret)
    {
        Assert.True(RemoteKeyReference.TryParse(secret.EncodedValue, out var reference));
        return reference.KeyId;
    }

    private static async ValueTask CreateTenantAsync(
        HttpClient client,
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/teams/{HostApplicationFactory.TeamId}/tenants",
            new
            {
                tenantId,
                displayName = tenantId,
                isDisabled = false,
                settings = new { },
            },
            cancellationToken
        );
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    private static async ValueTask PatchPlatformSettingAsync(
        HttpClient client,
        CancellationToken cancellationToken
    )
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

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, body);
    }

    private static async ValueTask<List<JsonElement>> GetJwksAsync(
        HttpClient client,
        string? host,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, JwksPath);
        if (host is not null)
        {
            request.Headers.Host = host;
        }

        using var response = await client.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        return [.. jsonElement.GetProperty("keys").EnumerateArray().Select(key => key.Clone())];
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
            {
                using var rsa = RSA.Create();
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
            {
                using var ecdsa = ECDsa.Create();
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
    private static async ValueTask<List<PersistedSecret>> WaitForSecretsAsync(
        HostApplicationFactory factory,
        string tenantId,
        int count,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var secrets = await GetStoredSecretsAsync(factory, tenantId, cancellationToken);
        while (secrets.Count < count)
        {
            Assert.True(
                stopwatch.Elapsed < TimeSpan.FromSeconds(15),
                $"The tenant '{tenantId}' holds {secrets.Count} of {count} secrets after 15 seconds."
            );
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
            secrets = await GetStoredSecretsAsync(factory, tenantId, cancellationToken);
        }

        return secrets;
    }

    private static async ValueTask<List<PersistedSecret>> GetStoredSecretsAsync(
        HostApplicationFactory factory,
        string tenantId,
        CancellationToken cancellationToken
    )
    {
        using var serviceScope = factory.Services.CreateScope();
        await using var storeManager = await serviceScope
            .ServiceProvider.GetRequiredService<IStoreManagerFactory>()
            .CreateAsync(cancellationToken);
        var secrets = await storeManager
            .GetStore<ITenantStore>()
            .GetSecretsAsync(tenantId, cancellationToken);
        return [.. secrets.Value];
    }
}
