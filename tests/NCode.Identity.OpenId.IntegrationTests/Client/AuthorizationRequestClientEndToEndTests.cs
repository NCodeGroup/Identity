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

using System.Collections.Concurrent;
using System.Net;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Client;

/// <summary>
/// Proves <see cref="IAuthorizationRequestClient"/> against the NCode OpenID provider hosted in-process: the client
/// pushes its authorization request to the provider's pushed authorization request endpoint (RFC 9126), and the
/// returned <c>request_uri</c> completes an authorization-code flow whose code the client redeems with its PKCE
/// verifier.
/// </summary>
public class AuthorizationRequestClientEndToEndTests
{
    private const string KeyRing = "par-e2e";
    private const string ClientSecret = "par-e2e-Wq4mZ8vK2nT7xR5bL9cY3hF6jD1sGp0E";
    private const string RedirectUri = "https://client.example/callback";
    private const string UserName = "par-e2e-user";
    private const string Password = "par-e2e-password";
    private const string State = "par-e2e-state";
    private const string Nonce = "par-e2e-nonce";
    private const string PushedAuthorizationRequestPath = "/oauth2/par";

    private static IReadOnlyDictionary<string, object> NoSettings { get; } =
        new Dictionary<string, object>();

    #region Scaffolding

    private static ServiceProvider CreateClientProvider(
        HostApplicationFactory factory,
        ConcurrentQueue<RecordedHttpExchange> exchanges,
        Func<ISecretKeyFactory, SecretKey> createKey
    )
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary().AddOpenIdClientLibrary();
        services
            .AddHttpClient(OpenIdClientDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler())
            .AddHttpMessageHandler(() => new RecordingHttpMessageHandler(exchanges));
        services.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            KeyRing,
            (serviceProvider, _) =>
                serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic([
                        createKey(serviceProvider.GetRequiredService<ISecretKeyFactory>()),
                    ])
        );
        return services.BuildServiceProvider();
    }

    private static SecretKey CreateSecretKey(ISecretKeyFactory keyFactory) =>
        keyFactory.CreateSymmetric(new KeyMetadata { KeyId = "s" }, ClientSecret.AsSpan());

    private static ClientRegistration CreateRegistration(
        string clientId,
        string authenticationMethod,
        bool requirePushedAuthorizationRequests = false
    ) =>
        new(HostApplicationFactory.TestServerIssuer, clientId, authenticationMethod)
        {
            KeyRing = KeyRing,
            RequirePushedAuthorizationRequests = requirePushedAuthorizationRequests,
        };

    // prompt=none lets the signed-in end-user's session answer the request without an interactive page.
    private static AuthorizationRequest CreateAuthorizationRequest() =>
        new(RedirectUri, State, TestPkce.CodeVerifier)
        {
            Nonce = Nonce,
            Scope = OpenIdProtocol.ScopeTypes.OpenId,
            AdditionalParameters = new Dictionary<string, string>
            {
                [OpenIdProtocol.Parameters.Prompt] = OpenIdProtocol.PromptTypes.None,
            },
        };

    private static async Task<AuthorizationRequestResult> PushAsync(
        ServiceProvider provider,
        ClientRegistration registration,
        AuthorizationRequest request
    ) =>
        await provider
            .GetRequiredService<IAuthorizationRequestClient>()
            .CreateAuthorizationUriAsync(registration, request, CancellationToken.None);

    // RFC 9126 §2.1, §2.2 and §4: one 201 push whose body carries client_id once and no request_uri, and an
    // authorization URL that carries only the client_id and the request_uri.
    private static RecordedHttpExchange AssertPushed(
        AuthorizationRequestResult result,
        ConcurrentQueue<RecordedHttpExchange> exchanges,
        string clientId
    )
    {
        Assert.True(result.IsSuccess, result.Error?.ToString());
        Assert.True(result.IsPushed);
        Assert.StartsWith(OpenIdProtocol.PushedAuthorizationRequestUriPrefix, result.RequestUri);
        Assert.True(result.ExpiresIn > 0);

        var push = Assert.Single(
            exchanges,
            exchange => exchange.Path == PushedAuthorizationRequestPath
        );
        Assert.Equal(HttpStatusCode.Created, push.StatusCode);

        var body = HttpUtility.ParseQueryString(push.Body ?? string.Empty);
        var clientIds = body.GetValues(OpenIdProtocol.Parameters.ClientId);
        Assert.NotNull(clientIds);
        Assert.Equal(clientId, Assert.Single(clientIds));
        Assert.Null(body.GetValues(OpenIdProtocol.Parameters.RequestUri));

        var query = HttpUtility.ParseQueryString(result.AuthorizationUri.Query);
        Assert.Equal(2, query.Count);
        Assert.Equal(clientId, query[OpenIdProtocol.Parameters.ClientId]);
        Assert.Equal(result.RequestUri, query[OpenIdProtocol.Parameters.RequestUri]);

        return push;
    }

    // Follows the authorization URL as the signed-in end-user's browser, then
    // redeems the code with the client and the verifier of the request.
    private static async Task AssertCodeFlowCompletesAsync(
        HostApplicationFactory factory,
        ServiceProvider provider,
        ClientRegistration registration,
        AuthorizationRequest request,
        AuthorizationRequestResult result
    )
    {
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
        Assert.StartsWith(request.RedirectUri, location.OriginalString, StringComparison.Ordinal);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal(request.State, parameters[OpenIdProtocol.Parameters.State]);
        var code = parameters[OpenIdProtocol.Parameters.AuthorizationCode];
        Assert.True(
            !string.IsNullOrEmpty(code),
            $"error='{parameters[OpenIdProtocol.Parameters.ErrorCode]}'"
        );

        var tokenResult = await provider
            .GetRequiredService<ITokenClient>()
            .RequestTokenAsync(
                registration,
                new AuthorizationCodeTokenRequest(code, request.RedirectUri, request.CodeVerifier),
                CancellationToken.None
            );

        Assert.True(tokenResult.IsSuccess, tokenResult.Error?.ToString());
        Assert.False(string.IsNullOrEmpty(tokenResult.Response.AccessToken));
        Assert.False(string.IsNullOrEmpty(tokenResult.Response.IdToken));
    }

    #endregion

    #region CreateAuthorizationUriAsync Tests

    [Fact]
    public async Task CreateAuthorizationUriAsync_WithClientSecretBasic_PushesAndCompletesTheCodeFlow()
    {
        const string clientId = "it-par-e2e-secret-basic";

        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedSharedSecretClientWithSettingsAsync(
            clientId,
            ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            NoSettings,
            RedirectUri
        );
        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        await using var provider = CreateClientProvider(factory, exchanges, CreateSecretKey);
        var registration = CreateRegistration(
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        );
        var request = CreateAuthorizationRequest();

        var result = await PushAsync(provider, registration, request);

        var push = AssertPushed(result, exchanges, clientId);
        Assert.Equal("Basic", push.AuthorizationScheme);
        await AssertCodeFlowCompletesAsync(factory, provider, registration, request, result);
    }

    [Fact]
    public async Task CreateAuthorizationUriAsync_WithClientSecretPost_PushesAndCompletesTheCodeFlow()
    {
        const string clientId = "it-par-e2e-secret-post";

        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedSharedSecretClientWithSettingsAsync(
            clientId,
            ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            NoSettings,
            RedirectUri
        );
        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        await using var provider = CreateClientProvider(factory, exchanges, CreateSecretKey);
        var registration = CreateRegistration(
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );
        var request = CreateAuthorizationRequest();

        var result = await PushAsync(provider, registration, request);

        var push = AssertPushed(result, exchanges, clientId);
        Assert.Null(push.AuthorizationScheme);
        var body = HttpUtility.ParseQueryString(push.Body ?? string.Empty);
        Assert.Equal(ClientSecret, body[OpenIdProtocol.Parameters.ClientSecret]);
        await AssertCodeFlowCompletesAsync(factory, provider, registration, request, result);
    }

    #endregion
}
