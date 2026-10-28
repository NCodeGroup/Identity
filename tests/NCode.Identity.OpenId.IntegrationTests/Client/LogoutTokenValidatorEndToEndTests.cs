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
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Jose;
using NCode.Identity.Jwt;
using NCode.Identity.OpenId.Client;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Client;

/// <summary>
/// Proves <see cref="ILogoutTokenValidator"/> against the NCode OpenID provider hosted in-process: an end-user signs
/// in and the client redeems a code for an ID token, the end-user logs out at the provider's end-session endpoint, and
/// the client validates the logout token the provider delivers to the client's <c>backchannel_logout_uri</c>.
/// </summary>
public class LogoutTokenValidatorEndToEndTests
{
    private const string KeyRing = "logout-e2e";
    private const string ClientSecret = "logout-e2e-Hn5vQ2xW8kR4mT7bY3cL9pF6jD1sGz0A";
    private const string RedirectUri = "https://client.example/callback";
    private const string BackChannelLogoutUri = "https://client.example/backchannel-logout";
    private const string BackChannelLogoutPath = "/backchannel-logout";
    private const string UserName = "logout-e2e-user";
    private const string Password = "logout-e2e-password";
    private const string State = "logout-e2e-state";
    private const string Nonce = "logout-e2e-nonce";
    private const string EndSessionPath = "/oauth2/end_session";

    #region Scaffolding

    private static ServiceProvider CreateClientProvider(HostApplicationFactory factory)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSecretsLibrary().AddJoseLibrary().AddJwtLibrary().AddOpenIdClientLibrary();
        // AddOpenIdClientLibrary registers no IExpiringKeySet for the logout-token validator.
        services.AddSharedStateServices().AddInProcessExpiringKeySet();
        services
            .AddHttpClient(OpenIdClientDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());
        services.AddKeyedSingleton<ISecretKeyCollectionProvider>(
            KeyRing,
            (serviceProvider, _) =>
                serviceProvider
                    .GetRequiredService<ISecretKeyCollectionProviderFactory>()
                    .CreateStatic([
                        serviceProvider
                            .GetRequiredService<ISecretKeyFactory>()
                            .CreateSymmetric(
                                new KeyMetadata { KeyId = "s" },
                                ClientSecret.AsSpan()
                            ),
                    ])
        );
        return services.BuildServiceProvider();
    }

    private static ClientRegistration CreateRegistration(string clientId) =>
        new(
            HostApplicationFactory.TestServerIssuer,
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic
        )
        {
            KeyRing = KeyRing,
        };

    private static Task SeedClientAsync(
        HostApplicationFactory factory,
        string clientId,
        IReadOnlyDictionary<string, object> settings
    ) =>
        factory.SeedSharedSecretClientWithSettingsAsync(
            clientId,
            ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            settings,
            RedirectUri
        );

    private static HttpClient CreateBrowser(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    // Follows the authorization URL as the signed-in end-user's browser (prompt=none lets the session answer without an
    // interactive page), redeems the code with the client and returns the verified payload of the ID token.
    private static async Task<JsonElement> RequestIdTokenAsync(
        HttpClient browser,
        string sessionCookie,
        ServiceProvider provider,
        ClientRegistration registration
    )
    {
        var request = new AuthorizationRequest(RedirectUri, State, TestPkce.CodeVerifier)
        {
            Nonce = Nonce,
            Scope = OpenIdProtocol.ScopeTypes.OpenId,
            AdditionalParameters = new Dictionary<string, string>
            {
                [OpenIdProtocol.Parameters.Prompt] = OpenIdProtocol.PromptTypes.None,
            },
        };
        var authorizationResult = await provider
            .GetRequiredService<IAuthorizationRequestClient>()
            .CreateAuthorizationUriAsync(registration, request, CancellationToken.None);
        Assert.True(authorizationResult.IsSuccess, authorizationResult.Error?.ToString());

        using var authorizeRequest = new HttpRequestMessage(
            HttpMethod.Get,
            authorizationResult.AuthorizationUri
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

        var tokenResult = await provider
            .GetRequiredService<ITokenClient>()
            .RequestTokenAsync(
                registration,
                new AuthorizationCodeTokenRequest(code, RedirectUri, TestPkce.CodeVerifier),
                CancellationToken.None
            );
        Assert.True(tokenResult.IsSuccess, tokenResult.Error?.ToString());
        var idToken = tokenResult.Response.IdToken;
        Assert.False(string.IsNullOrEmpty(idToken));

        var validation = await provider
            .GetRequiredService<IIdTokenValidator>()
            .ValidateAsync(
                registration,
                new IdTokenValidationRequest
                {
                    IdToken = idToken,
                    Nonce = Nonce,
                    AccessToken = tokenResult.Response.AccessToken,
                    AuthorizationCode = code,
                },
                CancellationToken.None
            );
        Assert.True(validation.IsValid, validation.FailureReason);
        return validation.Payload;
    }

    private static async Task EndSessionAsync(HttpClient browser, string sessionCookie)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, EndSessionPath);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        using var response = await browser.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The provider delivers the logout token on a background worker, so it arrives after the end-session response.
    private static async Task<string> WaitForLogoutTokenAsync(
        ConcurrentQueue<RecordedHttpExchange> exchanges
    )
    {
        var stopwatch = Stopwatch.StartNew();
        while (exchanges.IsEmpty && stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
        }

        var delivery = Assert.Single(exchanges);
        Assert.Equal(BackChannelLogoutPath, delivery.Path);

        var logoutToken = HttpUtility.ParseQueryString(delivery.Body ?? string.Empty)[
            OpenIdProtocol.Parameters.LogoutToken
        ];
        Assert.False(string.IsNullOrEmpty(logoutToken));
        return logoutToken;
    }

    private static async Task<LogoutTokenValidationResult> ValidateLogoutTokenAsync(
        ServiceProvider provider,
        ClientRegistration registration,
        string logoutToken
    ) =>
        await provider
            .GetRequiredService<ILogoutTokenValidator>()
            .ValidateAsync(
                registration,
                new LogoutTokenValidationRequest { LogoutToken = logoutToken },
                CancellationToken.None
            );

    private static string? GetString(JsonElement payload, string claimName) =>
        payload.GetProperty(claimName).GetString();

    #endregion

    #region ValidateAsync Tests

    [Fact]
    public async Task ValidateAsync_WithALogoutTokenTheProviderIssued_ReturnsItsTokenIdSubjectAndSession()
    {
        const string clientId = "it-logout-e2e-public";

        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        using var factory = new HostApplicationFactory { BackChannelLogoutExchanges = exchanges };
        await factory.SeedLocalAccountAsync(UserName, Password);
        await SeedClientAsync(
            factory,
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.BackChannelLogoutUri] = BackChannelLogoutUri,
            }
        );
        await using var provider = CreateClientProvider(factory);
        var registration = CreateRegistration(clientId);
        var browser = CreateBrowser(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(browser, UserName, Password);
        var idTokenPayload = await RequestIdTokenAsync(
            browser,
            sessionCookie,
            provider,
            registration
        );

        await EndSessionAsync(browser, sessionCookie);
        var logoutToken = await WaitForLogoutTokenAsync(exchanges);
        var result = await ValidateLogoutTokenAsync(provider, registration, logoutToken);

        Assert.True(result.IsValid, result.FailureReason);
        using var logoutTokenPayload = JsonDocument.Parse(
            Base64Url.DecodeFromChars(logoutToken.Split('.')[1])
        );
        Assert.Equal(
            GetString(logoutTokenPayload.RootElement, JoseClaimNames.Payload.Jti),
            result.TokenId
        );
        Assert.Equal(GetString(idTokenPayload, JoseClaimNames.Payload.Sub), result.Subject);
        Assert.Equal(GetString(idTokenPayload, JoseClaimNames.Payload.Sid), result.SessionId);
    }

    #endregion
}
