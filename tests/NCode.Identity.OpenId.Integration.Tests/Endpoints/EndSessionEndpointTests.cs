// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Exercises the OpenID Connect RP-Initiated Logout <c>end_session_endpoint</c> end-to-end: an interactive login
/// establishes an OP session cookie, and a <c>GET</c> to the end-session endpoint without a valid <c>id_token_hint</c>
/// asks the end-user to confirm. Once confirmed, the resumed logout runs the full logout pipeline (resolve
/// subject/session from the cookie, clear the OP login, revoke the session's grants, and — since no relying
/// party participated — skip back-channel notification), rendering the logged-out confirmation.
/// </summary>
public class EndSessionEndpointTests
{
    private const string UserName = "admin";
    private const string Password = "dev-password";

    private const string EndSessionPath = "/oauth2/end_session";
    private const string InteractiveCookieName = "__Host-NCode.Identity.Interactive";

    [Fact]
    public async Task GetEndSession_WhenSignedInWithoutAnIdTokenHint_RedirectsToTheConfirmationAndKeepsTheSession()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);

        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var request = new HttpRequestMessage(HttpMethod.Get, EndSessionPath);
        request.Headers.Add("Cookie", $"{InteractiveCookieName}={sessionCookie}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal(
            InteractiveLogin.LogoutConfirmPath,
            new Uri(
                new Uri("http://localhost"),
                response.Headers.Location.OriginalString
            ).AbsolutePath
        );

        // The session is left alone until the end-user confirms the logout.
        Assert.Null(ReadSetCookieRaw(response, InteractiveCookieName));
    }

    [Fact]
    public async Task ConfirmEndSession_WhenTheEndUserConfirms_ClearsTheSessionCookieAndRendersLoggedOut()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);

        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var response = await InteractiveLogin.ConfirmEndSessionAsync(
            client,
            EndSessionPath,
            sessionCookie
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("logged out", body, StringComparison.OrdinalIgnoreCase);

        // The OP login cookie is cleared (an expired Set-Cookie for the interactive scheme).
        var clearing = ReadSetCookieRaw(response, InteractiveCookieName);
        Assert.NotNull(clearing);
    }

    [Fact]
    public async Task ConfirmEndSession_WhenTheEndUserCancels_KeepsTheSession()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);

        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var cancel = await InteractiveLogin.AnswerEndSessionConfirmationAsync(
            client,
            EndSessionPath,
            sessionCookie,
            "cancel"
        );

        Assert.Equal(HttpStatusCode.Found, cancel.StatusCode);
        Assert.Equal("/account", cancel.Headers.Location?.OriginalString);

        using var me = new HttpRequestMessage(HttpMethod.Get, "/account/me");
        me.Headers.Add("Cookie", $"{InteractiveCookieName}={sessionCookie}");
        using var meResponse = await client.SendAsync(me);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Theory]
    [InlineData("logout")]
    [InlineData("cancel")]
    public async Task ConfirmEndSession_WithoutTheAntiforgeryToken_RefusesTheAnswerAndKeepsTheSession(
        string action
    )
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);

        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var challengeRequest = new HttpRequestMessage(HttpMethod.Get, EndSessionPath);
        challengeRequest.Headers.Add("Cookie", $"{InteractiveCookieName}={sessionCookie}");
        using var challenge = await client.SendAsync(challengeRequest);

        Assert.Equal(HttpStatusCode.Found, challenge.StatusCode);
        Assert.NotNull(challenge.Headers.Location);
        var location = new Uri(
            new Uri("http://localhost"),
            challenge.Headers.Location.OriginalString
        );
        var continueUrl = HttpUtility.ParseQueryString(location.Query)["ReturnUrl"];
        Assert.NotNull(continueUrl);

        // A cross-site form carries the session cookie but not the antiforgery token.
        using var answer = new HttpRequestMessage(
            HttpMethod.Post,
            InteractiveLogin.LogoutConfirmPath
        );
        answer.Headers.Add("Cookie", $"{InteractiveCookieName}={sessionCookie}");
        answer.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["action"] = action, ["ReturnUrl"] = continueUrl }
        );
        using var answerResponse = await client.SendAsync(answer);

        Assert.InRange((int)answerResponse.StatusCode, 400, 499);
        Assert.Null(ReadSetCookieRaw(answerResponse, InteractiveCookieName));

        using var me = new HttpRequestMessage(HttpMethod.Get, "/account/me");
        me.Headers.Add("Cookie", $"{InteractiveCookieName}={sessionCookie}");
        using var meResponse = await client.SendAsync(me);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);

        // The refused answer did not spend the continuation: the parked logout still resumes.
        var continueUri = new Uri(new Uri("http://localhost"), continueUrl);
        using var resume = new HttpRequestMessage(HttpMethod.Get, continueUri.PathAndQuery);
        resume.Headers.Add("Cookie", $"{InteractiveCookieName}={sessionCookie}");
        using var resumeResponse = await client.SendAsync(resume);

        Assert.Equal(HttpStatusCode.OK, resumeResponse.StatusCode);
    }

    [Fact]
    public async Task ContinueEndSession_WhenAnotherSessionIsSignedIn_RefusesTheLogout()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);

        var cookieA = await InteractiveLogin.SignInAsync(client, UserName, Password);
        var cookieB = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var challengeRequest = new HttpRequestMessage(HttpMethod.Get, EndSessionPath);
        challengeRequest.Headers.Add("Cookie", $"{InteractiveCookieName}={cookieA}");
        using var challenge = await client.SendAsync(challengeRequest);

        Assert.Equal(HttpStatusCode.Found, challenge.StatusCode);
        Assert.NotNull(challenge.Headers.Location);
        var location = new Uri(
            new Uri("http://localhost"),
            challenge.Headers.Location.OriginalString
        );
        var continueUrl = HttpUtility.ParseQueryString(location.Query)["ReturnUrl"];
        Assert.NotNull(continueUrl);

        // The continuation was parked for session A, so another browser's session cannot resume it.
        var continueUri = new Uri(new Uri("http://localhost"), continueUrl);
        using var resume = new HttpRequestMessage(HttpMethod.Get, continueUri.PathAndQuery);
        resume.Headers.Add("Cookie", $"{InteractiveCookieName}={cookieB}");
        using var resumeResponse = await client.SendAsync(resume);

        Assert.Equal(HttpStatusCode.BadRequest, resumeResponse.StatusCode);
    }

    [Fact]
    public async Task GetEndSession_WhenThePostLogoutRedirectUriIsNotAscii_RedirectsToItPercentEncoded()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedPublicClientWithSettingsAsync(
            "rp-escape",
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.PostLogoutRedirectUris] = new[]
                {
                    "https://rp.example/apr\u00e8s-logout",
                },
            },
            "https://rp.example/callback"
        );

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

        var postLogoutRedirectUri = Uri.EscapeDataString("https://rp.example/apr\u00e8s-logout");
        using var response = await client.GetAsync(
            $"{EndSessionPath}?client_id=rp-escape&post_logout_redirect_uri={postLogoutRedirectUri}"
        );

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(
            "https://rp.example/apr%C3%A8s-logout",
            response.Headers.Location?.OriginalString
        );
    }

    [Fact]
    public async Task GetEndSession_WhenThePostLogoutRedirectUriIsOfNoKind_RendersLoggedOut()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedPublicClientWithSettingsAsync(
            "rp-no-kind",
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.PostLogoutRedirectUris] = new[] { "myapp://logout" },
            },
            "https://rp.example/callback"
        );

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

        var postLogoutRedirectUri = Uri.EscapeDataString("myapp://logout");
        using var response = await client.GetAsync(
            $"{EndSessionPath}?client_id=rp-no-kind&post_logout_redirect_uri={postLogoutRedirectUri}"
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Null(response.Headers.Location);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("logged out", body, StringComparison.OrdinalIgnoreCase);
    }

    #region Login Helpers

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private static string? ReadSetCookieRaw(HttpResponseMessage response, string namePrefix)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            return null;
        }

        foreach (var cookie in cookies)
        {
            var firstPart = cookie.Split(';')[0];
            if (firstPart.StartsWith(namePrefix, StringComparison.Ordinal))
            {
                return firstPart;
            }
        }

        return null;
    }

    #endregion
}
