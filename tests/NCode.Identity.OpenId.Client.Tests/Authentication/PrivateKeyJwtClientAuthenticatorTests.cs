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

using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.Jose;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Credentials;
using NCode.Identity.Jwt;
using NCode.Identity.Jwt.Encoding;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Client.Authentication;
using NCode.Identity.OpenId.Client.Options;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Authentication;

public class PrivateKeyJwtClientAuthenticatorTests : BaseTests, IAsyncLifetime
{
    private const string Issuer = "https://op.example.test";
    private const string ClientId = "client-1";
    private const string Endpoint = "https://op.example.test/token";

    private ServiceProvider ServiceProvider { get; }
    private RSA Rsa { get; } = RSA.Create(2048);
    private SecretKey RsaKey { get; }

    public PrivateKeyJwtClientAuthenticatorTests()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary();
        ServiceProvider = serviceCollection.BuildServiceProvider();

        RsaKey = ServiceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateRsa(new KeyMetadata { KeyId = "k1" }, Rsa);
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        await ServiceProvider.DisposeAsync();
        Rsa.Dispose();
    }

    private PrivateKeyJwtClientAuthenticator CreateAuthenticator(
        string? keyRing,
        params SecretKey[] keys
    ) => CreateAuthenticator(new OpenIdClientOptions(), keyRing, keys);

    private PrivateKeyJwtClientAuthenticator CreateAuthenticator(
        OpenIdClientOptions options,
        string? keyRing,
        params SecretKey[] keys
    )
    {
        var mockKeyRingProvider = new Mock<IClientKeyRingProvider>(MockBehavior.Strict);
        if (keyRing is not null)
        {
            mockKeyRingProvider
                .Setup(x => x.GetKeyRing(keyRing))
                .Returns(
                    ServiceProvider.GetRequiredService<ISecretKeyCollectionFactory>().Create(keys)
                )
                .Verifiable();
        }

        return new PrivateKeyJwtClientAuthenticator(
            TimeProvider.System,
            mockKeyRingProvider.Object,
            ServiceProvider.GetRequiredService<IAlgorithmCollectionProvider>(),
            ServiceProvider.GetRequiredService<ICredentialSelector>(),
            ServiceProvider.GetRequiredService<IJwtEncoder>(),
            Microsoft.Extensions.Options.Options.Create(options)
        );
    }

    private static string GetAssertionAlgorithm(List<KeyValuePair<string, string>> parameters)
    {
        var assertion = Assert.Single(parameters, p => p.Key == "client_assertion").Value;
        var header = JsonElement.Parse(
            System.Buffers.Text.Base64Url.DecodeFromChars(assertion.Split('.')[0])
        );
        return header.GetProperty("alg").GetString()!;
    }

    private static ClientAuthenticationContext CreateContext(
        string? keyRing,
        List<string>? algorithms,
        List<KeyValuePair<string, string>> parameters
    ) =>
        new(
            new ClientRegistration(
                Issuer,
                ClientId,
                OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt
            )
            {
                KeyRing = keyRing,
            },
            new OpenIdProviderMetadata
            {
                Issuer = Issuer,
                TokenEndpointAuthSigningAlgValuesSupported = algorithms,
            },
            Endpoint,
            new HttpRequestMessage(HttpMethod.Post, Endpoint),
            parameters
        );

    private async Task AssertAssertionValidatesAsync(List<KeyValuePair<string, string>> parameters)
    {
        Assert.Contains(
            new KeyValuePair<string, string>(
                "client_assertion_type",
                "urn:ietf:params:oauth:client-assertion-type:jwt-bearer"
            ),
            parameters
        );
        Assert.Contains(new KeyValuePair<string, string>("client_id", ClientId), parameters);
        var assertion = Assert.Single(parameters, p => p.Key == "client_assertion").Value;

        var validator = ServiceProvider.GetRequiredService<IJwtValidator>();
        var result = await validator.ValidateAsync(
            assertion,
            new JwtValidationParameters { ValidSignatureAlgorithms = ["PS256"] }
                .UseValidationKeys([RsaKey])
                .ValidateIssuer(ClientId)
                .ValidateAudience(Issuer)
                .ValidateTokenLifeTime(),
            CancellationToken.None
        );
        Assert.True(result.IsValid);

        var payloadSegment = assertion.Split('.')[1];
        var payload = JsonElement.Parse(
            System.Buffers.Text.Base64Url.DecodeFromChars(payloadSegment)
        );
        var root = payload;
        Assert.Equal(ClientId, root.GetProperty("sub").GetString());
        Assert.Equal(ClientId, root.GetProperty("iss").GetString());
        Assert.Equal(JsonValueKind.String, root.GetProperty("aud").ValueKind);
        Assert.Equal(Issuer, root.GetProperty("aud").GetString());
        Assert.False(string.IsNullOrEmpty(root.GetProperty("jti").GetString()));
        Assert.Equal(60, root.GetProperty("exp").GetInt64() - root.GetProperty("iat").GetInt64());
    }

    #region MethodName Tests

    [Fact]
    public void MethodName_Always_IsPrivateKeyJwt()
    {
        var authenticator = CreateAuthenticator(keyRing: null);

        Assert.Equal(
            OpenIdProtocol.ClientAuthenticationMethods.PrivateKeyJwt,
            authenticator.MethodName
        );
    }

    #endregion

    #region AuthenticateAsync Tests

    [Fact]
    public async Task AuthenticateAsync_WithProviderAlgorithms_SignsWithPreferredSupportedAlgorithm()
    {
        var authenticator = CreateAuthenticator("ring", RsaKey);
        var parameters = new List<KeyValuePair<string, string>>();
        var context = CreateContext("ring", ["HS256", "RS256", "PS256"], parameters);

        await authenticator.AuthenticateAsync(context, CancellationToken.None);

        await AssertAssertionValidatesAsync(parameters);
    }

    [Fact]
    public async Task AuthenticateAsync_WithoutProviderAlgorithms_SignsWithPreferredAlgorithm()
    {
        var authenticator = CreateAuthenticator("ring", RsaKey);
        var parameters = new List<KeyValuePair<string, string>>();
        var context = CreateContext("ring", algorithms: null, parameters);

        await authenticator.AuthenticateAsync(context, CancellationToken.None);

        await AssertAssertionValidatesAsync(parameters);
        Assert.Equal("PS256", GetAssertionAlgorithm(parameters));
    }

    [Fact]
    public async Task AuthenticateAsync_WithConfiguredAlgorithm_SignsWithConfiguredAlgorithm()
    {
        var authenticator = CreateAuthenticator(
            new OpenIdClientOptions { ClientAssertionSigningAlgorithms = ["RS256"] },
            "ring",
            RsaKey
        );
        var parameters = new List<KeyValuePair<string, string>>();
        var context = CreateContext("ring", algorithms: null, parameters);

        await authenticator.AuthenticateAsync(context, CancellationToken.None);

        Assert.Equal("RS256", GetAssertionAlgorithm(parameters));
    }

    [Fact]
    public async Task AuthenticateAsync_WithConfiguredAlgorithmWithoutCompatibleKey_Throws()
    {
        var authenticator = CreateAuthenticator(
            new OpenIdClientOptions { ClientAssertionSigningAlgorithms = ["ES256"] },
            "ring",
            RsaKey
        );
        var context = CreateContext("ring", algorithms: null, []);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await authenticator.AuthenticateAsync(context, CancellationToken.None)
        );
    }

    [Fact]
    public async Task AuthenticateAsync_WithOnlySymmetricProviderAlgorithm_Throws()
    {
        var symmetricKey = ServiceProvider
            .GetRequiredService<ISecretKeyFactory>()
            .CreateSymmetric(new KeyMetadata { KeyId = "s1" }, new byte[32]);
        var authenticator = CreateAuthenticator("ring", RsaKey, symmetricKey);
        var context = CreateContext("ring", ["HS256"], []);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await authenticator.AuthenticateAsync(context, CancellationToken.None)
        );
    }

    [Fact]
    public async Task AuthenticateAsync_WithNullKeyRing_Throws()
    {
        var authenticator = CreateAuthenticator(keyRing: null);
        var context = CreateContext(keyRing: null, algorithms: null, []);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await authenticator.AuthenticateAsync(context, CancellationToken.None)
        );
    }

    #endregion
}
