// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// The interactive surface renders in the first of the authorization request's <c>ui_locales</c> that it serves and
/// the tenant's <c>ui_locales_supported</c> lists, otherwise in the tenant's default language, and a preference it
/// cannot honor never fails the request (OpenID Connect Core 1.0 §3.1.2.1).
/// </summary>
// See ADR-1402.
public class InteractiveUiLocalesTests
{
    private const string ClientId = "it-ui-locales";
    private const string RedirectUri = "https://client.example/callback";
    private const string LoginPath = "/account/login";

    private static readonly Dictionary<string, object> EnglishAndFrench = new()
    {
        [OpenIdSettingNames.UiLocalesSupported] = new[] { "en", "fr" },
    };

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
        string? acceptLanguage = null
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (acceptLanguage is not null)
            request.Headers.AcceptLanguage.ParseAdd(acceptLanguage);

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
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
