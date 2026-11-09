// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.Jwk;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.Secrets.KeyTypes;
using NCode.Identity.Secrets.RemoteKeys;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Client;

/// <summary>
/// Proves the OpenID client library signs its <c>private_key_jwt</c> assertion and its DPoP proofs with keys that a
/// key management service holds, against the NCode OpenID provider hosted in-process.
/// </summary>
// See ADR-1479.
public class RemoteClientKeyEndToEndTests
{
    private const string ClientKeyRing = "remote-client-keys";
    private const string DPoPKeyRing = "remote-dpop-keys";
    private const string JwksUri = "https://client.remote-keys.integration.test/jwks.json";
    private const string ResourceServerUri = "https://api.remote-keys.integration.test";

    #region Scaffolding

    private static ServiceProvider CreateClientProvider(
        HostApplicationFactory factory,
        TestRemoteKeyProvider kms,
        Func<SecretKey> clientKey,
        Func<SecretKey> dpopKey
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary().AddOpenIdClientLibrary();
        services
            .AddHttpClient(OpenIdClientDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());
        services.AddSingleton<IRemoteKeyProvider>(kms);
        services.AddSingleton<IRemoteKeyDescriptorSource>(kms);
        AddRing(services, ClientKeyRing, clientKey);
        AddRing(services, DPoPKeyRing, dpopKey);
        return services.BuildServiceProvider();
    }

    private static void AddRing(ServiceCollection services, string name, Func<SecretKey> key) =>
        services.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            name,
            (serviceProvider, _) =>
                serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic([key()])
        );

    private static async Task<SecretKey> ReadAndProveAsync(
        ServiceProvider provider,
        string kmsKeyId,
        string keyId
    )
    {
        var resolver = provider.GetRequiredService<IRemoteKeyResolver>();
        var resolution = resolver.Resolve(
            new RemoteKeyAlgorithmSelector
            {
                Provider = TestRemoteKeyProvider.ProviderName,
                Algorithm = "ES256",
                SecretType = SecretKeyTypes.Ecc,
                KeySizeBits = 256,
                Curve = "P-256",
            }
        );
        var key = await resolver.ReadKeyAsync(
            resolution.Bind(kmsKeyId),
            new KeyMetadata { KeyId = keyId },
            CancellationToken.None
        );
        await provider
            .GetRequiredService<IRemoteKeyProver>()
            .ProveAsync(key, CancellationToken.None);
        return key;
    }

    private static string ExportJwks(ServiceProvider provider, SecretKey key)
    {
        Assert.True(
            provider
                .GetRequiredService<ISecretKeyTypeCatalog>()
                .TryGet(key.KeyType, out var descriptor)
        );
        var exported = descriptor.ExportPublicKey(key);
        Assert.NotNull(exported);
        return new JsonWebKeySet([exported.Jwk]).ToJsonString();
    }

    // RFC 7638: the SHA-256 of the required members of the public key in lexicographic order.
    private static string ComputeThumbprint(ECDsa key)
    {
        var parameters = key.ExportParameters(false);
        var json =
            $$"""{"crv":"P-256","kty":"EC","x":"{{Base64Url.EncodeToString(parameters.Q.X)}}","y":"{{Base64Url.EncodeToString(parameters.Q.Y)}}"}""";
        return Base64Url.EncodeToString(SHA256.HashData(SecureEncoding.UTF8.GetBytes(json)));
    }

    #endregion

    #region RequestTokenAsync Tests

    [Fact]
    public async Task RequestTokenAsync_WithKeysAKmsHolds_SignsTheAssertionAndTheProofInTheKms()
    {
        const string clientId = "remote-key-client";

        using var kms = new TestRemoteKeyProvider();
        var clientEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var dpopEcdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var dpopThumbprint = ComputeThumbprint(dpopEcdsa);
        var clientKmsKeyId = kms.AddKey(clientEcdsa);
        var dpopKmsKeyId = kms.AddKey(dpopEcdsa);

        SecretKey? clientKey = null;
        SecretKey? dpopKey = null;
        string? jwks = null;

        using var factory = new HostApplicationFactory
        {
            ClientJwksResponder = _ =>
                jwks ?? throw new InvalidOperationException("The client's key set is not ready."),
        };
        await factory.SeedConfidentialClientWithJwksUriAsync(
            clientId,
            JwksUri,
            OpenIdProtocol.GrantTypes.ClientCredentials
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            clientId + "-rs",
            ResourceServerUri,
            TestPlatformSettingsProvider.ApiScope
        );

        await using var provider = CreateClientProvider(
            factory,
            kms,
            () => clientKey ?? throw new InvalidOperationException("The client key is not read."),
            () => dpopKey ?? throw new InvalidOperationException("The DPoP key is not read.")
        );
        clientKey = await ReadAndProveAsync(provider, clientKmsKeyId, "client-kms");
        dpopKey = await ReadAndProveAsync(provider, dpopKmsKeyId, "dpop-kms");
        jwks = ExportJwks(provider, clientKey);

        var signaturesBefore = kms.SignatureCount;
        var registration = new ClientRegistration(
            HostApplicationFactory.TestServerIssuer,
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt
        )
        {
            KeyRing = ClientKeyRing,
            DPoPKeyRing = DPoPKeyRing,
        };

        var result = await provider
            .GetRequiredService<ITokenClient>()
            .RequestTokenAsync(
                registration,
                new ClientCredentialsTokenRequest { Scope = TestPlatformSettingsProvider.ApiScope },
                CancellationToken.None
            );

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.Equal(OpenIdProtocol.TokenTypes.DPoP, result.Response.TokenType, ignoreCase: true);
        Assert.Equal(dpopThumbprint, result.DPoPJwkThumbprint);
        Assert.True(kms.SignatureCount - signaturesBefore >= 2);
    }

    #endregion
}
