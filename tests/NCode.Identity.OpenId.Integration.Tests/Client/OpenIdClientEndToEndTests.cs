// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Encoders;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Client;

/// <summary>
/// Proves the OpenID client library against the NCode OpenID provider hosted in-process.
/// </summary>
public class OpenIdClientEndToEndTests
{
    private const string KeyRing = "e2e";
    private const string KeyId = "e2e-key";
    private const string JwksUri = "https://client.e2e.integration.test/jwks.json";
    private const string ResourceServerUri = "https://api.client.integration.test";
    private const string ClientSecret = "e2e-Zq7vN3pLw9XkT2mRb8YcD5fHjA4sGu6E";

    private static string BuildJwks(RSA rsa)
    {
        var parameters = rsa.ExportParameters(includePrivateParameters: false);
        var jwk = new Dictionary<string, object>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["alg"] = "PS256",
            ["kid"] = KeyId,
            ["n"] = Base64Url.Encode(parameters.Modulus!),
            ["e"] = Base64Url.Encode(parameters.Exponent!),
        };
        var jwks = new Dictionary<string, object> { ["keys"] = new[] { jwk } };
        return JsonSerializer.Serialize(jwks);
    }

    private static ServiceProvider CreateClientProvider(
        HostApplicationFactory factory,
        Func<ISecretKeyFactory, SecretKey> createKey
    )
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection
            .AddSecretsLibrary()
            .AddJoseLibrary()
            .AddJwtLibrary()
            .AddOpenIdClientLibrary();
        serviceCollection
            .AddHttpClient(OpenIdClientDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());
        serviceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            KeyRing,
            (serviceProvider, _) =>
                serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic([
                        createKey(serviceProvider.GetRequiredService<ISecretKeyFactory>()),
                    ])
        );
        return serviceCollection.BuildServiceProvider();
    }

    private static async ValueTask SeedResourceServerAsync(
        HostApplicationFactory factory,
        string clientId,
        CancellationToken cancellationToken
    ) =>
        await factory.SeedResourceServerWithClientGrantAsync(
            clientId,
            clientId + "-rs",
            ResourceServerUri,
            [TestPlatformSettingsProvider.ApiScope],
            cancellationToken
        );

    private static ClientRegistration CreateSecretPostRegistration(string clientId) =>
        new(
            HostApplicationFactory.TestServerIssuer,
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        )
        {
            KeyRing = KeyRing,
        };

    private static SecretKey CreateSecretKey(ISecretKeyFactory keyFactory) =>
        keyFactory.CreateSymmetric(new KeyMetadata { KeyId = "s" }, ClientSecret.AsSpan());

    [Fact]
    public async Task RequestTokenAsync_PrivateKeyJwt_ReturnsAccessToken()
    {
        const string clientId = "e2e-private-key-jwt-client";
        using var rsa = RSA.Create(2048);
        var jwks = BuildJwks(rsa);

        using var factory = new HostApplicationFactory { ClientJwksResponder = _ => jwks };
        await factory.SeedConfidentialClientWithJwksUriAsync(
            clientId,
            JwksUri,
            [OpenIdProtocol.GrantTypes.ClientCredentials],
            CancellationToken.None
        );
        await SeedResourceServerAsync(factory, clientId, CancellationToken.None);

        await using var provider = CreateClientProvider(
            factory,
            keyFactory =>
                keyFactory.CreateRsa(
                    new KeyMetadata
                    {
                        KeyId = KeyId,
                        Use = "sig",
                        Algorithm = "PS256",
                    },
                    rsa
                )
        );
        var registration = new ClientRegistration(
            HostApplicationFactory.TestServerIssuer,
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt
        )
        {
            KeyRing = KeyRing,
        };

        var result = await provider
            .GetRequiredService<ITokenClient>()
            .RequestTokenAsync(
                registration,
                new ClientCredentialsTokenRequest { Scope = TestPlatformSettingsProvider.ApiScope },
                CancellationToken.None
            );

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.False(string.IsNullOrEmpty(result.Response.AccessToken));
    }

    [Fact]
    public async Task RequestTokenAsync_ClientSecretPost_ReturnsAccessToken()
    {
        const string clientId = "e2e-secret-post-client";

        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            clientId,
            ClientSecret,
            [OpenIdProtocol.GrantTypes.ClientCredentials],
            CancellationToken.None
        );
        await SeedResourceServerAsync(factory, clientId, CancellationToken.None);

        await using var provider = CreateClientProvider(factory, CreateSecretKey);

        var result = await provider
            .GetRequiredService<ITokenClient>()
            .RequestTokenAsync(
                CreateSecretPostRegistration(clientId),
                new ClientCredentialsTokenRequest { Scope = TestPlatformSettingsProvider.ApiScope },
                CancellationToken.None
            );

        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.False(string.IsNullOrEmpty(result.Response.AccessToken));
    }

    [Fact]
    public async Task RevokeTokenAsync_AccessToken_ReturnsNull()
    {
        const string clientId = "e2e-revoke-client";

        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            clientId,
            ClientSecret,
            [OpenIdProtocol.GrantTypes.ClientCredentials],
            CancellationToken.None
        );
        await SeedResourceServerAsync(factory, clientId, CancellationToken.None);

        await using var provider = CreateClientProvider(factory, CreateSecretKey);
        var registration = CreateSecretPostRegistration(clientId);

        var tokenResult = await provider
            .GetRequiredService<ITokenClient>()
            .RequestTokenAsync(
                registration,
                new ClientCredentialsTokenRequest { Scope = TestPlatformSettingsProvider.ApiScope },
                CancellationToken.None
            );
        Assert.True(tokenResult.IsSuccess, tokenResult.Error?.ToString());
        var accessToken = tokenResult.Response.AccessToken;
        Assert.False(string.IsNullOrEmpty(accessToken));

        var error = await provider
            .GetRequiredService<ITokenRevocationClient>()
            .RevokeTokenAsync(
                registration,
                new TokenRevocationRequest(accessToken)
                {
                    TokenTypeHint = OpenIdProtocol.TokenTypeHints.AccessToken,
                },
                CancellationToken.None
            );

        Assert.Null(error);
    }

    [Fact]
    public async Task GetSigningKeysAsync_Provider_ReturnsKeys()
    {
        using var factory = new HostApplicationFactory();

        await using var provider = CreateClientProvider(factory, CreateSecretKey);

        var keys = await provider
            .GetRequiredService<IProviderKeySetResolver>()
            .GetSigningKeysAsync(HostApplicationFactory.TestServerIssuer, CancellationToken.None);

        Assert.True(keys.Count >= 1);
    }
}
