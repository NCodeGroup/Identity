// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Specialized;
using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// The interactive surface renders in the first of the authorization or end-session request's <c>ui_locales</c> that
/// it serves and the tenant's <c>ui_locales_supported</c> lists, otherwise in the tenant's default language, a
/// preference it cannot honor never fails the request (OpenID Connect Core 1.0 §3.1.2.1, RP-Initiated Logout 1.0 §2),
/// and every page the surface redirects to renders in the locale the form or the request carried.
/// </summary>
// See ADR-1402.
public class InteractiveUiLocalesTests
{
    private const string ClientId = "it-ui-locales";
    private const string RedirectUri = "https://client.example/callback";
    private const string UserName = "ui-locales-user";
    private const string Password = "ui-locales-password";
    private const string LoginPath = "/account/login";
    private const string AccountPath = "/account";
    private const string ConsentPath = "/account/consent";
    private const string EndSessionPath = "/oauth2/end_session";

    private static Dictionary<string, object> EnglishAndFrench { get; } =
        new() { [OpenIdSettingNames.UiLocalesSupported] = new[] { "en", "fr" } };

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private static string AuthorizeUrl(string? uiLocales) =>
        "/oauth2/authorize"
        + $"?client_id={Uri.EscapeDataString(ClientId)}"
        + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
        + "&response_type=code"
        + "&scope=openid"
        + "&state=abc123"
        + TestPkce.QueryParameters
        + (uiLocales is null ? string.Empty : $"&ui_locales={Uri.EscapeDataString(uiLocales)}");

    private static async Task<Uri> GetLoginRedirectAsync(HttpClient client, string? uiLocales)
    {
        using var response = await client.GetAsync(AuthorizeUrl(uiLocales));

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var loginUrl = new Uri(new Uri("http://localhost"), location);
        Assert.Equal(LoginPath, loginUrl.AbsolutePath);
        return loginUrl;
    }

    private static async Task<string> GetPageAsync(
        HttpClient client,
        string url,
        string? acceptLanguage = null,
        string? sessionCookie = null
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (acceptLanguage is not null)
            request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);
        if (sessionCookie is not null)
            request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    private static async Task<Uri> GetRedirectAsync(
        HttpClient client,
        string url,
        string sessionCookie,
        string expectedPath
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var baseUri = new Uri("http://localhost");
        var redirect = response.Headers.Location.OriginalString;
        var location = new Uri(baseUri, redirect);
        Assert.Equal(expectedPath, location.AbsolutePath);
        return location;
    }

    [Fact]
    public async Task Authorize_WhenTheRequestPrefersASupportedLocale_TheLoginPageRendersInIt()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        await factory.SeedPublicClientAsync(ClientId, RedirectUri);
        using var client = CreateClient(factory);

        var loginUrl = await GetLoginRedirectAsync(client, "fr");
        Assert.Equal("fr", HttpUtility.ParseQueryString(loginUrl.Query)["ui_locales"]);

        var html = await GetPageAsync(client, loginUrl.PathAndQuery);

        Assert.Contains("<html lang=\"fr\">", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Se connecter</h1>", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ui_locales\" value=\"fr\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorize_WhenTheRequestPrefersAnUnsupportedLocale_NeverFailsAndRendersTheDefault()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        await factory.SeedPublicClientAsync(ClientId, RedirectUri);
        using var client = CreateClient(factory);

        var loginUrl = await GetLoginRedirectAsync(client, "xx-YY");
        Assert.Equal("xx-YY", HttpUtility.ParseQueryString(loginUrl.Query)["ui_locales"]);

        var html = await GetPageAsync(client, loginUrl.PathAndQuery);

        Assert.Contains("<html lang=\"en\">", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Sign in</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorize_WhenTheRequestListsLocalesInOrder_RendersTheFirstSupportedOne()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        await factory.SeedPublicClientAsync(ClientId, RedirectUri);
        using var client = CreateClient(factory);

        // German is served but the tenant does not support it; a regional French tag selects French.
        var loginUrl = await GetLoginRedirectAsync(client, "xx de fr-CA en");

        var html = await GetPageAsync(client, loginUrl.PathAndQuery);

        Assert.Contains("<html lang=\"fr\">", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Se connecter</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorize_WhenTheTenantSupportsNoLocaleInParticular_RendersAnyServedLocale()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedPublicClientAsync(ClientId, RedirectUri);
        using var client = CreateClient(factory);

        var loginUrl = await GetLoginRedirectAsync(client, "de");

        var html = await GetPageAsync(client, loginUrl.PathAndQuery);

        Assert.Contains("<html lang=\"de\">", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Anmelden</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLoginPage_WhenTheTenantSetsADefaultLocale_RendersInItWithoutAPreference()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(
            new Dictionary<string, object> { [OpenIdSettingNames.DefaultUiLocale] = "es" }
        );
        using var client = CreateClient(factory);

        var html = await GetPageAsync(client, LoginPath);

        Assert.Contains("<html lang=\"es\">", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Iniciar sesi&#xF3;n</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLoginPage_WhenTheTenantSupportsOnlyALaterRegisteredLocale_RendersInItWithoutAPreference()
    {
        // The advertised set bounds the fallback too, so the page never renders in a locale the tenant left out.
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.UiLocalesSupported] = new[] { "fr" },
            }
        );
        using var client = CreateClient(factory);

        var html = await GetPageAsync(client, LoginPath);

        Assert.Contains("<html lang=\"fr\">", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Se connecter</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLoginPage_WhenOnlyTheUserAgentStatesALanguage_RendersInItWhenSupported()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        using var client = CreateClient(factory);

        var html = await GetPageAsync(client, LoginPath, acceptLanguage: "de;q=0.9, fr;q=0.8");

        Assert.Contains("<html lang=\"fr\">", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostLogin_WhenTheCredentialsAreWrong_RedirectsBackInTheSameLocale()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        using var client = CreateClient(factory);

        using var response = await InteractiveLogin.PostFormAsync(
            client,
            $"{LoginPath}?ReturnUrl=%2Ffoo&ui_locales=fr",
            LoginPath,
            sessionCookie: null,
            new Dictionary<string, string>
            {
                ["username"] = "nobody",
                ["password"] = "wrong",
                ["ReturnUrl"] = "/foo",
                ["ui_locales"] = "fr",
            }
        );

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = new Uri(new Uri("http://localhost"), response.Headers.Location);
        var query = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal("invalid_credentials", query["error"]);
        Assert.Equal("fr", query["ui_locales"]);

        var html = await GetPageAsync(client, location.PathAndQuery);
        Assert.Contains(
            "Le nom d&#x2019;utilisateur ou le mot de passe est incorrect.",
            html,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task PostLogin_WhenACredentialIsMissing_RedirectsBackInTheSameLocale()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        using var client = CreateClient(factory);

        using var response = await InteractiveLogin.PostFormAsync(
            client,
            $"{LoginPath}?ReturnUrl=%2Ffoo&ui_locales=fr",
            LoginPath,
            sessionCookie: null,
            new Dictionary<string, string>
            {
                ["username"] = "nobody",
                ["ReturnUrl"] = "/foo",
                ["ui_locales"] = "fr",
            }
        );

        var query = AssertRedirectsToTheLoginPage(response);
        Assert.Equal("/foo", query["ReturnUrl"]);
        Assert.Equal("invalid_request", query["error"]);
        Assert.Equal("fr", query["ui_locales"]);
    }

    [Fact]
    public async Task PostConsent_WhenNoSessionExists_RedirectsToTheLoginPageInTheSameLocale()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        using var client = CreateClient(factory);

        using var response = await InteractiveLogin.PostFormAsync(
            client,
            $"{ConsentPath}?ReturnUrl=%2Ffoo&ui_locales=fr",
            ConsentPath,
            sessionCookie: null,
            new Dictionary<string, string>
            {
                ["action"] = "allow",
                ["ReturnUrl"] = "/foo",
                ["ui_locales"] = "fr",
            }
        );

        var query = AssertRedirectsToTheLoginPage(response);
        Assert.Equal("/foo", query["ReturnUrl"]);
        Assert.Equal("invalid_request", query["error"]);
        Assert.Equal("fr", query["ui_locales"]);
    }

    [Fact]
    public async Task PostConsent_WhenTheReturnUrlIsMissing_RedirectsToTheLoginPageInTheSameLocale()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        await factory.SeedLocalAccountAsync(UserName, Password);
        using var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var response = await InteractiveLogin.PostFormAsync(
            client,
            $"{ConsentPath}?ui_locales=fr",
            ConsentPath,
            sessionCookie,
            new Dictionary<string, string> { ["action"] = "allow", ["ui_locales"] = "fr" }
        );

        var query = AssertRedirectsToTheLoginPage(response);
        Assert.Equal("/", query["ReturnUrl"]);
        Assert.Equal("invalid_request", query["error"]);
        Assert.Equal("fr", query["ui_locales"]);
    }

    // The query of the login page a form post redirects back to: the page's return URL, the error it shows and the
    // locale it renders in.
    private static NameValueCollection AssertRedirectsToTheLoginPage(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = new Uri(
            new Uri("http://localhost"),
            response.Headers.Location.OriginalString
        );
        Assert.Equal(LoginPath, location.AbsolutePath);
        return HttpUtility.ParseQueryString(location.Query);
    }

    [Theory]
    [InlineData(
        "invalid_credentials",
        "Le nom d&#x2019;utilisateur ou le mot de passe est incorrect."
    )]
    [InlineData("invalid_request", "Saisissez un nom d&#x2019;utilisateur et un mot de passe.")]
    [InlineData(
        "access_denied",
        "Ce compte ne peut pas se connecter. Contactez votre administrateur."
    )]
    [InlineData("server_error", "La connexion a &#xE9;chou&#xE9;. Veuillez r&#xE9;essayer.")]
    public async Task GetLoginPage_WhenTheRequestCarriesAnErrorCode_ShowsTheLocalesMessageForIt(
        string error,
        string expectedMessage
    )
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        using var client = CreateClient(factory);

        var html = await GetPageAsync(
            client,
            $"{LoginPath}?ReturnUrl=%2F&error={error}&ui_locales=fr"
        );

        Assert.Contains("<html lang=\"fr\">", html, StringComparison.Ordinal);
        Assert.Contains(expectedMessage, html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetAuthorize_WhenTheRequestIsRefusedAndPrefersASupportedLocale_TheErrorPageRendersInIt()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        using var client = CreateClient(factory);

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "/oauth2/authorize?client_id=unknown-client&ui_locales=fr"
        );
        request.Headers.Accept.ParseAdd("text/html");
        using var response = await client.SendAsync(request);
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<html lang=\"fr\">", html, StringComparison.Ordinal);
        Assert.Contains(
            "<title>Erreur d&#x2019;autorisation</title>",
            html,
            StringComparison.Ordinal
        );
        Assert.Contains("<h1>Erreur d&#x2019;autorisation</h1>", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorize_WhenConsentIsRequiredAndTheRequestPrefersASupportedLocale_TheConsentPageRendersInIt()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientWithSettingsAsync(
            ClientId,
            new Dictionary<string, object> { [OpenIdSettingNames.RequireConsent] = true },
            RedirectUri
        );
        using var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        var consentUrl = await GetRedirectAsync(
            client,
            AuthorizeUrl("fr"),
            sessionCookie,
            ConsentPath
        );
        Assert.Equal("fr", HttpUtility.ParseQueryString(consentUrl.Query)["ui_locales"]);

        var html = await GetPageAsync(
            client,
            consentUrl.PathAndQuery,
            sessionCookie: sessionCookie
        );

        Assert.Contains("<html lang=\"fr\">", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Autoriser l&#x2019;acc&#xE8;s</h1>", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ui_locales\" value=\"fr\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndSession_WhenTheRequestPrefersASupportedLocale_TheConfirmationPageRendersInIt()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        await factory.SeedLocalAccountAsync(UserName, Password);
        using var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        var confirmUrl = await GetRedirectAsync(
            client,
            $"{EndSessionPath}?ui_locales=fr",
            sessionCookie,
            InteractiveLogin.LogoutConfirmPath
        );
        Assert.Equal("fr", HttpUtility.ParseQueryString(confirmUrl.Query)["ui_locales"]);

        var html = await GetPageAsync(
            client,
            confirmUrl.PathAndQuery,
            sessionCookie: sessionCookie
        );

        Assert.Contains("<html lang=\"fr\">", html, StringComparison.Ordinal);
        Assert.Contains("<h1>Se d&#xE9;connecter</h1>", html, StringComparison.Ordinal);
        Assert.Contains("name=\"ui_locales\" value=\"fr\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostLogoutConfirmation_WhenTheEndUserDeclines_ReturnsToTheAccountPageInTheSameLocale()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        await factory.SeedLocalAccountAsync(UserName, Password);
        using var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);
        var confirmUrl = await GetRedirectAsync(
            client,
            $"{EndSessionPath}?ui_locales=fr",
            sessionCookie,
            InteractiveLogin.LogoutConfirmPath
        );
        var returnUrl = HttpUtility.ParseQueryString(confirmUrl.Query)["ReturnUrl"];
        Assert.NotNull(returnUrl);

        using var response = await InteractiveLogin.PostFormAsync(
            client,
            confirmUrl.PathAndQuery,
            InteractiveLogin.LogoutConfirmPath,
            sessionCookie,
            new Dictionary<string, string>
            {
                ["action"] = "cancel",
                ["ReturnUrl"] = returnUrl,
                ["ui_locales"] = "fr",
            }
        );

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = new Uri(
            new Uri("http://localhost"),
            response.Headers.Location.OriginalString
        );
        Assert.Equal(AccountPath, location.AbsolutePath);
        Assert.Equal("fr", HttpUtility.ParseQueryString(location.Query)["ui_locales"]);
    }

    [Fact]
    public async Task GetAccountPage_WhenAnonymousAndTheRequestCarriesALocale_RedirectsToTheLoginPageInIt()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync($"{AccountPath}?ui_locales=fr");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var location = new Uri(
            new Uri("http://localhost"),
            response.Headers.Location.OriginalString
        );
        Assert.Equal(LoginPath, location.AbsolutePath);
        var query = HttpUtility.ParseQueryString(location.Query);
        Assert.Equal($"{AccountPath}?ui_locales=fr", query["ReturnUrl"]);
        Assert.Equal("fr", query["ui_locales"]);
    }

    [Fact]
    public async Task Discovery_WhenTheTenantSupportsLocales_AdvertisesThem()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(EnglishAndFrench);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync()
        );

        Assert.Equal(
            ["en", "fr"],
            document
                .GetProperty(OpenIdSettingNames.UiLocalesSupported)
                .EnumerateArray()
                .Select(element => element.GetString())
        );
    }

    [Fact]
    public async Task Discovery_WhenTheTenantSupportsNoLocaleInParticular_AdvertisesNone()
    {
        using var factory = new HostApplicationFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = JsonSerializer.Deserialize<JsonElement>(
            await response.Content.ReadAsStringAsync()
        );

        Assert.False(document.TryGetProperty(OpenIdSettingNames.UiLocalesSupported, out _));
    }
}
