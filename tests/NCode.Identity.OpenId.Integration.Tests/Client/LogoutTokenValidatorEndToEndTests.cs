// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Collections;
using NCode.Identity.Secrets.KeyMaterial;
using NCode.Identity.Secrets.Keys;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Client;

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
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection
            .AddSecretsLibrary()
            .AddJoseLibrary()
            .AddJwtLibrary()
            .AddOpenIdClientLibrary();
        // AddOpenIdClientLibrary registers no IExpiringKeySet for the logout-token validator.
        serviceCollection.AddSharedStateServices().AddInProcessExpiringKeySet();
        serviceCollection
            .AddHttpClient(OpenIdClientDefaults.HttpClientName)
            .ConfigurePrimaryHttpMessageHandler(() => factory.Server.CreateHandler());
        serviceCollection.AddKeyedSingleton<ISecretKeyCollectionProvider>(
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
        return serviceCollection.BuildServiceProvider();
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

    private static ValueTask SeedClientAsync(
        HostApplicationFactory factory,
        string clientId,
        IReadOnlyDictionary<string, object> settings,
        CancellationToken cancellationToken
    ) =>
        factory.SeedSharedSecretClientWithSettingsAsync(
            clientId,
            ClientSecret,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretBasic,
            settings,
            [RedirectUri],
            cancellationToken
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
    private static async ValueTask<JsonElement> RequestIdTokenAsync(
        HttpClient browser,
        string sessionCookie,
        ServiceProvider provider,
        ClientRegistration registration,
        CancellationToken cancellationToken
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
            .CreateAuthorizationUriAsync(registration, request, cancellationToken);
        Assert.True(authorizationResult.IsSuccess, authorizationResult.Error?.ToString());

        using var authorizeRequest = new HttpRequestMessage(
            HttpMethod.Get,
            authorizationResult.AuthorizationUri
        );
        authorizeRequest.Headers.Add(
            "Cookie",
            $"{InteractiveLogin.SessionCookieName}={sessionCookie}"
        );
        using var authorizeResponse = await browser.SendAsync(authorizeRequest, cancellationToken);

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
                cancellationToken
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
                cancellationToken
            );
        Assert.True(validation.IsValid, validation.FailureReason);
        return validation.Payload;
    }

    private static async ValueTask EndSessionAsync(
        HttpClient browser,
        string sessionCookie,
        CancellationToken cancellationToken
    )
    {
        using var response = await InteractiveLogin.ConfirmEndSessionAsync(
            browser,
            EndSessionPath,
            sessionCookie,
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    // The provider delivers the logout token on a background worker, so it arrives after the end-session response.
    private static async ValueTask<string> WaitForLogoutTokenAsync(
        ConcurrentQueue<RecordedHttpExchange> exchanges,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        while (exchanges.IsEmpty && stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }

        var delivery = Assert.Single(exchanges);
        Assert.Equal(BackChannelLogoutPath, delivery.Path);

        var logoutToken = HttpUtility.ParseQueryString(delivery.Body ?? string.Empty)[
            OpenIdProtocol.Parameters.LogoutToken
        ];
        Assert.False(string.IsNullOrEmpty(logoutToken));
        return logoutToken;
    }

    private static async ValueTask<LogoutTokenValidationResult> ValidateLogoutTokenAsync(
        ServiceProvider provider,
        ClientRegistration registration,
        string logoutToken,
        CancellationToken cancellationToken
    ) =>
        await provider
            .GetRequiredService<ILogoutTokenValidator>()
            .ValidateAsync(
                registration,
                new LogoutTokenValidationRequest { LogoutToken = logoutToken },
                cancellationToken
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
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await SeedClientAsync(
            factory,
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.BackChannelLogoutUri] = BackChannelLogoutUri,
            },
            CancellationToken.None
        );
        await using var provider = CreateClientProvider(factory);
        var registration = CreateRegistration(clientId);
        var browser = CreateBrowser(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            browser,
            UserName,
            Password,
            CancellationToken.None
        );
        var idTokenPayload = await RequestIdTokenAsync(
            browser,
            sessionCookie,
            provider,
            registration,
            CancellationToken.None
        );

        await EndSessionAsync(browser, sessionCookie, CancellationToken.None);
        var logoutToken = await WaitForLogoutTokenAsync(exchanges, CancellationToken.None);
        var result = await ValidateLogoutTokenAsync(
            provider,
            registration,
            logoutToken,
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Equal(HostApplicationFactory.TestServerIssuer, result.Issuer);
        var logoutTokenPayload = JsonElement.Parse(
            Base64Url.DecodeFromChars(logoutToken.Split('.')[1])
        );
        Assert.Equal(GetString(logoutTokenPayload, JoseClaimNames.Payload.Jti), result.TokenId);
        Assert.Equal(GetString(idTokenPayload, JoseClaimNames.Payload.Sub), result.Subject);
        Assert.Equal(GetString(idTokenPayload, JoseClaimNames.Payload.Sid), result.SessionId);
    }

    [Fact]
    public async Task ValidateAsync_WithAPairwiseClient_ReturnsTheSubjectOfItsIdToken()
    {
        const string publicClientId = "it-logout-e2e-public-control";
        const string pairwiseClientId = "it-logout-e2e-pairwise";

        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        using var factory = new HostApplicationFactory { BackChannelLogoutExchanges = exchanges };
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await SeedClientAsync(
            factory,
            publicClientId,
            new Dictionary<string, object>(),
            CancellationToken.None
        );
        await SeedClientAsync(
            factory,
            pairwiseClientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.BackChannelLogoutUri] = BackChannelLogoutUri,
                [OpenIdSettingNames.SubjectType] = OpenIdProtocol.SubjectTypes.Pairwise,
            },
            CancellationToken.None
        );
        await using var provider = CreateClientProvider(factory);
        var publicRegistration = CreateRegistration(publicClientId);
        var pairwiseRegistration = CreateRegistration(pairwiseClientId);
        var browser = CreateBrowser(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            browser,
            UserName,
            Password,
            CancellationToken.None
        );
        var publicPayload = await RequestIdTokenAsync(
            browser,
            sessionCookie,
            provider,
            publicRegistration,
            CancellationToken.None
        );
        var pairwisePayload = await RequestIdTokenAsync(
            browser,
            sessionCookie,
            provider,
            pairwiseRegistration,
            CancellationToken.None
        );
        var pairwiseSubject = GetString(pairwisePayload, JoseClaimNames.Payload.Sub);
        Assert.NotEqual(GetString(publicPayload, JoseClaimNames.Payload.Sub), pairwiseSubject);

        await EndSessionAsync(browser, sessionCookie, CancellationToken.None);
        var logoutToken = await WaitForLogoutTokenAsync(exchanges, CancellationToken.None);
        var result = await ValidateLogoutTokenAsync(
            provider,
            pairwiseRegistration,
            logoutToken,
            CancellationToken.None
        );

        Assert.True(result.IsValid, result.FailureReason);
        Assert.Equal(pairwiseSubject, result.Subject);
    }

    [Fact]
    public async Task ValidateAsync_WhenTheLogoutTokenIsValidatedAgainBeforeItIsRecorded_IsValid()
    {
        const string clientId = "it-logout-e2e-retry";

        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        using var factory = new HostApplicationFactory { BackChannelLogoutExchanges = exchanges };
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await SeedClientAsync(
            factory,
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.BackChannelLogoutUri] = BackChannelLogoutUri,
            },
            CancellationToken.None
        );
        await using var provider = CreateClientProvider(factory);
        var registration = CreateRegistration(clientId);
        var browser = CreateBrowser(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            browser,
            UserName,
            Password,
            CancellationToken.None
        );
        await RequestIdTokenAsync(
            browser,
            sessionCookie,
            provider,
            registration,
            CancellationToken.None
        );

        await EndSessionAsync(browser, sessionCookie, CancellationToken.None);
        var logoutToken = await WaitForLogoutTokenAsync(exchanges, CancellationToken.None);
        var first = await ValidateLogoutTokenAsync(
            provider,
            registration,
            logoutToken,
            CancellationToken.None
        );
        var second = await ValidateLogoutTokenAsync(
            provider,
            registration,
            logoutToken,
            CancellationToken.None
        );

        Assert.True(first.IsValid, first.FailureReason);
        Assert.True(second.IsValid, second.FailureReason);
    }

    [Fact]
    public async Task ValidateAsync_WhenTheLogoutTokenWasRecordedAsHandled_FailsAsAReplay()
    {
        const string clientId = "it-logout-e2e-replay";

        var exchanges = new ConcurrentQueue<RecordedHttpExchange>();
        using var factory = new HostApplicationFactory { BackChannelLogoutExchanges = exchanges };
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await SeedClientAsync(
            factory,
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.BackChannelLogoutUri] = BackChannelLogoutUri,
            },
            CancellationToken.None
        );
        await using var provider = CreateClientProvider(factory);
        var registration = CreateRegistration(clientId);
        var browser = CreateBrowser(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(
            browser,
            UserName,
            Password,
            CancellationToken.None
        );
        await RequestIdTokenAsync(
            browser,
            sessionCookie,
            provider,
            registration,
            CancellationToken.None
        );

        await EndSessionAsync(browser, sessionCookie, CancellationToken.None);
        var logoutToken = await WaitForLogoutTokenAsync(exchanges, CancellationToken.None);
        var validator = provider.GetRequiredService<ILogoutTokenValidator>();
        var first = await ValidateLogoutTokenAsync(
            provider,
            registration,
            logoutToken,
            CancellationToken.None
        );
        Assert.True(first.IsValid, first.FailureReason);

        var recorded = await validator.TryRecordHandledAsync(
            registration,
            first,
            CancellationToken.None
        );
        var second = await ValidateLogoutTokenAsync(
            provider,
            registration,
            logoutToken,
            CancellationToken.None
        );
        var recordedAgain = await validator.TryRecordHandledAsync(
            registration,
            first,
            CancellationToken.None
        );

        Assert.True(recorded);
        Assert.False(second.IsValid);
        Assert.Equal(TokenValidationFailureCodes.TokenReplayed, second.FailureCode);
        Assert.False(recordedAgain);
    }

    #endregion
}
