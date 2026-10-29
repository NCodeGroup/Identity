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
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Client;

/// <summary>
/// Proves <see cref="ITokenClient"/> binds tokens with DPoP (RFC 9449) against the NCode OpenID provider hosted
/// in-process: the client pushes <c>dpop_jkt</c> with its authorization request, answers the provider's
/// <c>use_dpop_nonce</c> challenge with a fresh proof, and the issued access token is bound to the proof key.
/// </summary>
public class TokenClientDPoPEndToEndTests
{
    private const string SecretKeyRing = "dpop-e2e-secret";
    private const string KeyRingA = "dpop-e2e-a";
    private const string KeyRingB = "dpop-e2e-b";
    private const string RotatedKeyRing = "dpop-e2e-rotated";
    private const string ClientSecret = "dpop-e2e-Wq4mZ8vK2nT7xR5bL9cY3hF6jD1sGp0E";
    private const string RedirectUri = "https://client.example/callback";
    private const string UserName = "dpop-e2e-user";
    private const string Password = "dpop-e2e-password";
    private const string State = "dpop-e2e-state";
    private const string Nonce = "dpop-e2e-nonce";
    private const string TokenPath = "/oauth2/token";
    private const string ParPath = "/oauth2/par";

    #region Scaffolding

    private static ServiceProvider CreateClientProvider(
        HostApplicationFactory factory,
        ConcurrentQueue<RecordedHttpExchange> exchanges,
        ECDsa keyA,
        ECDsa keyB
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary().AddOpenIdClientLibrary();
        services
            .AddHttpClient(OpenIdClientDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler())
            .AddHttpMessageHandler(() => new RecordingHttpMessageHandler(exchanges));

        AddRing(
            services,
            SecretKeyRing,
            keyFactory =>
                [keyFactory.CreateSymmetric(new KeyMetadata { KeyId = "s" }, ClientSecret.AsSpan())]
        );
        AddRing(
            services,
            KeyRingA,
            keyFactory => [keyFactory.CreateEcc(new KeyMetadata { KeyId = $"{KeyRingA}-a" }, keyA)]
        );
        AddRing(
            services,
            KeyRingB,
            keyFactory => [keyFactory.CreateEcc(new KeyMetadata { KeyId = $"{KeyRingB}-b" }, keyB)]
        );
        AddRing(
            services,
            RotatedKeyRing,
            keyFactory =>
                [
                    keyFactory.CreateEcc(new KeyMetadata { KeyId = $"{RotatedKeyRing}-b" }, keyB),
                    keyFactory.CreateEcc(new KeyMetadata { KeyId = $"{RotatedKeyRing}-a" }, keyA),
                ]
        );
        return services.BuildServiceProvider();
    }

    private static void AddRing(
        ServiceCollection services,
        string name,
        Func<ISecretKeyFactory, SecretKey[]> createKeys
    ) =>
        services.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            name,
            (serviceProvider, _) =>
                serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic(
                        createKeys(serviceProvider.GetRequiredService<ISecretKeyFactory>())
                    )
        );

    private static ClientRegistration CreateRegistration(string clientId, string dpopKeyRing) =>
        new(
            HostApplicationFactory.TestServerIssuer,
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        )
        {
            KeyRing = SecretKeyRing,
            DPoPKeyRing = dpopKeyRing,
        };

    private static async Task SeedClientAsync(HostApplicationFactory factory, string clientId)
    {
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedSharedSecretClientWithSettingsAsync(
            clientId,
            ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            new Dictionary<string, object> { [OpenIdSettingNames.DPoPNonceRequired] = true },
            RedirectUri
        );
    }

    // prompt=none lets the signed-in end-user's session answer the request without an interactive page.
    private static async Task<(AuthorizationRequestResult Result, string Code)> AuthorizeAsync(
        HostApplicationFactory factory,
        ServiceProvider provider,
        ClientRegistration registration
    )
    {
        var request = new AuthorizationRequest(RedirectUri, State, TestPkce.CodeVerifier)
        {
            Nonce = Nonce,
            Scope = $"{OpenIdProtocol.ScopeTypes.OpenId} {OpenIdProtocol.ScopeTypes.OfflineAccess}",
            AdditionalParameters = new Dictionary<string, string>
            {
                [OpenIdProtocol.Parameters.Prompt] = OpenIdProtocol.PromptTypes.None,
            },
        };
        var result = await provider
            .GetRequiredService<IAuthorizationRequestClient>()
            .CreateAuthorizationUriAsync(registration, request, CancellationToken.None);
        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.NotNull(result.AuthorizationUri);

        var browser = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var sessionCookie = await InteractiveLogin.SignInAsync(browser, UserName, Password);

        using var authorizeRequest = new HttpRequestMessage(
            HttpMethod.Get,
            result.AuthorizationUri
        );
        authorizeRequest.Headers.Add(
            "Cookie",
            $"{InteractiveLogin.SessionCookieName}={sessionCookie}"
        );
        using var authorizeResponse = await browser.SendAsync(authorizeRequest);

        var location = authorizeResponse.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.OriginalString, StringComparison.Ordinal);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal(State, parameters[OpenIdProtocol.Parameters.State]);
        var code = parameters[OpenIdProtocol.Parameters.AuthorizationCode];
        Assert.True(
            !string.IsNullOrEmpty(code),
            $"error='{parameters[OpenIdProtocol.Parameters.ErrorCode]}'"
        );

        return (result, code);
    }

    private static async Task<TokenResult> RedeemAsync(
        ServiceProvider provider,
        ClientRegistration registration,
        string code
    ) =>
        await provider
            .GetRequiredService<ITokenClient>()
            .RequestTokenAsync(
                registration,
                new AuthorizationCodeTokenRequest(code, RedirectUri, TestPkce.CodeVerifier),
                CancellationToken.None
            );

    // RFC 7638: the SHA-256 of the required members of the public key in lexicographic order.
    private static string ComputeThumbprint(ECDsa key)
    {
        var parameters = key.ExportParameters(false);
        var json =
            $$"""{"crv":"P-256","kty":"EC","x":"{{Base64Url.EncodeToString(parameters.Q.X)}}","y":"{{Base64Url.EncodeToString(parameters.Q.Y)}}"}""";
        return Base64Url.EncodeToString(SHA256.HashData(SecureEncoding.UTF8.GetBytes(json)));
    }

    private static JsonElement DecodePayload(string? jwt)
    {
        Assert.False(string.IsNullOrEmpty(jwt));
        var segments = jwt.Split('.');
        Assert.Equal(3, segments.Length);

        return JsonElement.Parse(
            SecureEncoding.UTF8.GetString(Base64Url.DecodeFromChars(segments[1]))
        );
    }

    #endregion

    #region RequestTokenAsync Tests

    [Fact]
    public async Task RequestTokenAsync_WhenTheProviderRequiresANonce_RetriesOnceAndBindsTheTokenToTheKey()
    {
        const string clientId = "it-dpop-e2e-nonce";

        using var keyA = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var keyB = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        using var factory = new HostApplicationFactory();
        await SeedClientAsync(factory, clientId);
        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        await using var provider = CreateClientProvider(factory, exchanges, keyA, keyB);
        var registration = CreateRegistration(clientId, KeyRingA);
        var thumbprint = ComputeThumbprint(keyA);

        var metadata = await provider
            .GetRequiredService<IProviderMetadataResolver>()
            .GetMetadataAsync(registration.Authority, CancellationToken.None);
        Assert.Contains("ES256", metadata.DPoPSigningAlgValuesSupported ?? []);

        var (result, code) = await AuthorizeAsync(factory, provider, registration);
        Assert.Equal(thumbprint, result.DPoPJwkThumbprint);

        var push = Assert.Single(exchanges, exchange => exchange.Path == ParPath);
        var pushBody = HttpUtility.ParseQueryString(push.Body ?? string.Empty);
        Assert.Equal(thumbprint, pushBody[OpenIdProtocol.Parameters.DPoPJwkThumbprint]);
        Assert.Null(push.DPoPProof);

        var tokenResult = await RedeemAsync(provider, registration, code);

        Assert.True(tokenResult.IsSuccess, tokenResult.Error?.ToString());
        Assert.Equal(
            OpenIdProtocol.TokenTypes.DPoP,
            tokenResult.Response.TokenType,
            ignoreCase: true
        );
        Assert.Equal(thumbprint, tokenResult.DPoPJwkThumbprint);

        var tokenExchanges = exchanges.Where(exchange => exchange.Path == TokenPath).ToList();
        Assert.Equal(2, tokenExchanges.Count);
        var challenge = tokenExchanges[0];
        var retry = tokenExchanges[1];

        Assert.Equal(HttpStatusCode.BadRequest, challenge.StatusCode);
        Assert.NotNull(challenge.ResponseBody);
        Assert.Equal(
            OpenIdProtocol.ErrorCodes.UseDPoPNonce,
            JsonElement
                .Parse(challenge.ResponseBody)
                .GetProperty(OpenIdProtocol.Parameters.ErrorCode)
                .GetString()
        );
        Assert.False(string.IsNullOrEmpty(challenge.DPoPNonce));
        var challengeProof = DecodePayload(challenge.DPoPProof);
        Assert.False(challengeProof.TryGetProperty("nonce", out _));

        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        var retryProof = DecodePayload(retry.DPoPProof);
        Assert.Equal(challenge.DPoPNonce, retryProof.GetProperty("nonce").GetString());
        Assert.NotEqual(
            challengeProof.GetProperty("jti").GetString(),
            retryProof.GetProperty("jti").GetString()
        );

        var accessToken = DecodePayload(tokenResult.Response.AccessToken);
        Assert.Equal(
            thumbprint,
            accessToken
                .GetProperty("cnf")
                .GetProperty(OpenIdProtocol.ConfirmationMethods.JwkThumbprint)
                .GetString()
        );
    }

    #endregion
}
