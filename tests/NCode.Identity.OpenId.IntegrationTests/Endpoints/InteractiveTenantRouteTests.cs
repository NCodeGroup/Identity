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

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
using NCode.Identity.OpenId.Authentication.Interactive;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Proves that the interactive login, consent, and account surface is served beneath the tenant route, like the
/// protocol endpoints (ADR-0193): under the path strategy each tenant's surface lives beneath its own path, its
/// redirects stay beneath that path, and its session cookie is scoped to it so tenants sharing an origin keep separate
/// sessions.
/// </summary>
public class InteractiveTenantRouteTests
{
    private const string UserName = "alice";
    private const string Password = "alice-password";
    private const string SessionCookieName = InteractiveLogin.SessionCookieName;
    private const string AntiforgeryRequestCookieName = "XSRF-TOKEN";
    private const string AntiforgeryFieldName = "__RequestVerificationToken";
    private const string AntiforgeryCookiePrefix = ".AspNetCore.Antiforgery";

    private const string BundleIndexHtml =
        "<!doctype html><html><head><script type=\"module\" src=\"./assets/app.js\"></script></head>"
        + "<body><div id=\"root\"></div></body></html>";

    private const string BundleScript = "console.log('app');";

    private static ConfiguredHostApplicationFactory CreatePathFactory(
        string? bundleRootPath = null
    ) =>
        new(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                    .TenantStrategyCodes
                    .DynamicByPath,
                ["OpenId:TenantResolution:DynamicByPath:IssuerTemplate"] =
                    "http://localhost/{tenantId}",
                ["InteractiveAuthentication:BundleRootPath"] = bundleRootPath,
            }
        );

    private static DirectoryInfo CreateBundle()
    {
        var bundleRoot = Directory.CreateTempSubdirectory("bundle-");
        File.WriteAllText(
            Path.Combine(bundleRoot.FullName, "index.html"),
            BundleIndexHtml,
            SecureEncoding.UTF8
        );
        var assets = bundleRoot.CreateSubdirectory("assets");
        File.WriteAllText(
            Path.Combine(assets.FullName, "app.js"),
            BundleScript,
            SecureEncoding.UTF8
        );
        return bundleRoot;
    }

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    #region Routing Tests

    [Fact]
    public async Task GetLogin_WhenTenantIsSelectedByPath_IsServedBeneathTheTenantRoute()
    {
        using var factory = CreatePathFactory();
        await SeedTenantAsync(factory, "acme");
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/acme/account/login?ReturnUrl=%2Facme%2Ffoo");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        // The form posts relative to the page, so it reaches the login endpoint of the tenant that served it.
        Assert.Contains("method=\"post\" action=\"login\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetLogin_WhenTenantIsSelectedByPath_IsNotServedAtTheRoot()
    {
        using var factory = CreatePathFactory();
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/account/login");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetAccount_WhenAnonymousUnderAPathTenant_RedirectsToThatTenantsLoginPage()
    {
        using var factory = CreatePathFactory();
        await SeedTenantAsync(factory, "acme");
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/acme/account");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(
            "/acme/account/login?ReturnUrl=%2Facme%2Faccount",
            response.Headers.Location?.OriginalString
        );
    }

    [Fact]
    public async Task GetAccount_WhenRequestedWithATrailingSlash_IsServedAsTheAccountPage()
    {
        using var factory = CreatePathFactory();
        await SeedTenantAsync(factory, "acme");
        using var client = CreateClient(factory);

        // The bundle links to the account page as its base, `/acme/account/`.
        using var response = await client.GetAsync("/acme/account/");

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.StartsWith(
            "/acme/account/login?ReturnUrl=",
            response.Headers.Location?.OriginalString,
            StringComparison.Ordinal
        );
    }

    #endregion

    #region Bundle Tests

    [Fact]
    public async Task GetLogin_WhenBundlePresent_SetsTheDocumentBaseToTheTenantsSurface()
    {
        var bundleRoot = CreateBundle();
        try
        {
            using var factory = CreatePathFactory(bundleRoot.FullName);
            await SeedTenantAsync(factory, "acme");
            using var client = CreateClient(factory);

            using var response = await client.GetAsync("/acme/account/login");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.Content.ReadAsStringAsync();
            Assert.StartsWith(
                "<!doctype html><html><head><base href=\"/acme/account/\" /><script",
                html,
                StringComparison.Ordinal
            );
        }
        finally
        {
            bundleRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetAsset_WhenTenantIsSelectedByPath_ServesTheBundleFileBeneathTheTenantRoute()
    {
        var bundleRoot = CreateBundle();
        try
        {
            using var factory = CreatePathFactory(bundleRoot.FullName);
            await SeedTenantAsync(factory, "acme");
            using var client = CreateClient(factory);

            using var response = await client.GetAsync("/acme/account/assets/app.js");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(BundleScript, await response.Content.ReadAsStringAsync());
            var entityTag = response.Headers.ETag;
            Assert.NotNull(entityTag);

            // The validator lets the browser revalidate the file without downloading it again.
            using var revalidation = new HttpRequestMessage(
                HttpMethod.Get,
                "/acme/account/assets/app.js"
            );
            revalidation.Headers.IfNoneMatch.Add(entityTag);
            using var revalidated = await client.SendAsync(revalidation);
            Assert.Equal(HttpStatusCode.NotModified, revalidated.StatusCode);
        }
        finally
        {
            bundleRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetAsset_WhenTheTenantIsUnknown_ServesTheFileWithoutResolvingTheTenant()
    {
        var bundleRoot = CreateBundle();
        try
        {
            using var factory = CreatePathFactory(bundleRoot.FullName);
            using var client = CreateClient(factory);

            using var response = await client.GetAsync("/nobody/account/assets/app.js");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(BundleScript, await response.Content.ReadAsStringAsync());
        }
        finally
        {
            bundleRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetAsset_WhenTheFileIsServed_LetsTheBrowserKeepItForAYear()
    {
        var bundleRoot = CreateBundle();
        try
        {
            using var factory = CreatePathFactory(bundleRoot.FullName);
            await SeedTenantAsync(factory, "acme");
            using var client = CreateClient(factory);

            using var response = await client.GetAsync("/acme/account/assets/app.js");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var cacheControl = response.Headers.CacheControl;
            Assert.NotNull(cacheControl);
            Assert.True(cacheControl.Public);
            Assert.Equal(TimeSpan.FromDays(365), cacheControl.MaxAge);
            Assert.Contains(cacheControl.Extensions, extension => extension.Name == "immutable");
        }
        finally
        {
            bundleRoot.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("/account/assets/app.js")]
    [InlineData("/acme/account/assets/missing.js")]
    [InlineData("/acme/account/assets")]
    [InlineData("/acme/account/index.html")]
    public async Task GetAsset_WhenThePathNamesNoFileOfTheTenantsBundle_ReturnsNotFound(string path)
    {
        var bundleRoot = CreateBundle();
        try
        {
            using var factory = CreatePathFactory(bundleRoot.FullName);
            await SeedTenantAsync(factory, "acme");
            using var client = CreateClient(factory);

            using var response = await client.GetAsync(path);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Null(response.Headers.CacheControl);
        }
        finally
        {
            bundleRoot.Delete(recursive: true);
        }
    }

    #endregion

    #region Session Cookie Tests

    [Fact]
    public async Task PostLogin_WhenTenantIsSelectedByPath_ScopesTheSessionToThatTenant()
    {
        using var factory = CreatePathFactory();
        await SeedTenantAsync(factory, "acme");
        await SeedTenantAsync(factory, "beta");
        await SeedLocalAccountAsync(factory, "acme");
        using var client = CreateClient(factory);

        using var login = await SignInAsync(client, "/acme");

        Assert.Equal(HttpStatusCode.Found, login.StatusCode);
        Assert.Equal("/acme/foo", login.Headers.Location?.OriginalString);
        var setCookie = ReadSetCookie(login, SessionCookieName);
        Assert.NotNull(setCookie);
        Assert.Contains("path=/acme", setCookie, StringComparison.OrdinalIgnoreCase);

        var sessionCookie = setCookie.Split(';')[0];
        using var acmeMe = await GetWithCookieAsync(client, "/acme/account/me", sessionCookie);
        Assert.Equal(HttpStatusCode.OK, acmeMe.StatusCode);
    }

    [Fact]
    public async Task PostLogout_WhenTenantIsSelectedByPath_ClearsTheTenantsCookieAndReturnsToItsLoginPage()
    {
        using var factory = CreatePathFactory();
        await SeedTenantAsync(factory, "acme");
        await SeedLocalAccountAsync(factory, "acme");
        using var client = CreateClient(factory);
        using var login = await SignInAsync(client, "/acme");
        var sessionCookie = ReadSetCookie(login, SessionCookieName)!.Split(';')[0];

        // Mint antiforgery tokens bound to the authenticated session.
        using var loginPage = await GetWithCookieAsync(
            client,
            "/acme/account/login",
            sessionCookie
        );
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        var requestToken = ReadSetCookieValue(loginPage, AntiforgeryRequestCookieName);
        var cookieToken = ReadSetCookie(loginPage, AntiforgeryCookiePrefix)?.Split(';')[0];
        Assert.NotNull(requestToken);
        Assert.NotNull(cookieToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/acme/account/logout");
        request.Headers.Add("Cookie", $"{sessionCookie}; {cookieToken}");
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string> { [AntiforgeryFieldName] = requestToken }
        );
        using var logout = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Found, logout.StatusCode);
        Assert.Equal("/acme/account/login", logout.Headers.Location?.OriginalString);
        var clearing = ReadSetCookie(logout, SessionCookieName);
        Assert.NotNull(clearing);
        Assert.Contains("path=/acme", clearing, StringComparison.OrdinalIgnoreCase);
    }

    #endregion

    #region Branding Tests

    [Fact]
    public async Task GetLogin_WhenBundlePresent_EmbedsTheTenantsBranding()
    {
        var bundleRoot = CreateBundle();
        try
        {
            await using var factory = CreatePathFactory(bundleRoot.FullName);
            await SeedTenantAsync(
                factory,
                "acme",
                new Dictionary<string, object>
                {
                    [InteractiveSettingNames.BrandingDisplayName] = "Acme",
                    [InteractiveSettingNames.BrandingPrimaryColor] = "#1565c0",
                    [InteractiveSettingNames.BrandingColorMode] = "dark",
                }
            );
            await SeedTenantAsync(factory, "beta");
            using var client = CreateClient(factory);

            using var acmeResponse = await client.GetAsync("/acme/account/login");
            using var betaResponse = await client.GetAsync("/beta/account/login");

            Assert.Equal(HttpStatusCode.OK, acmeResponse.StatusCode);
            // The embedded branding is as fresh as the page: every page that issues antiforgery tokens is sent
            // no-cache, no-store (ADR-0253).
            Assert.True(acmeResponse.Headers.CacheControl?.NoStore);
            Assert.True(acmeResponse.Headers.CacheControl?.NoCache);
            Assert.Contains(
                "<script type=\"application/json\" id=\"branding\">"
                    + "{\"displayName\":\"Acme\",\"primaryColor\":\"#1565c0\",\"mode\":\"dark\"}</script>",
                await acmeResponse.Content.ReadAsStringAsync(),
                StringComparison.Ordinal
            );
            Assert.Equal(HttpStatusCode.OK, betaResponse.StatusCode);
            Assert.Contains(
                "<script type=\"application/json\" id=\"branding\">{}</script>",
                await betaResponse.Content.ReadAsStringAsync(),
                StringComparison.Ordinal
            );
        }
        finally
        {
            bundleRoot.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task GetAuthorize_WhenTheErrorPageIsServedWithTheBundle_EmbedsTheTenantsBranding()
    {
        var bundleRoot = CreateBundle();
        try
        {
            await using var factory = CreatePathFactory(bundleRoot.FullName);
            await SeedTenantAsync(
                factory,
                "acme",
                new Dictionary<string, object>
                {
                    [InteractiveSettingNames.BrandingDisplayName] = "Acme",
                }
            );
            using var client = CreateClient(factory);

            using var request = new HttpRequestMessage(
                HttpMethod.Get,
                $"/acme/oauth2/authorize?{OpenIdConstants.Parameters.ClientId}=unknown-client"
            );
            request.Headers.Accept.ParseAdd("text/html");
            using var response = await client.SendAsync(request);

            var html = await response.Content.ReadAsStringAsync();
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Contains(
                "<base href=\"/acme/account/\" /><script type=\"application/json\" id=\"branding\">"
                    + "{\"displayName\":\"Acme\"}</script>",
                html,
                StringComparison.Ordinal
            );
            Assert.Contains(
                $"data-error-code=\"{OpenIdConstants.ErrorCodes.InvalidClient}\"",
                html,
                StringComparison.Ordinal
            );
        }
        finally
        {
            bundleRoot.Delete(recursive: true);
        }
    }

    #endregion

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string tenantPath)
    {
        var loginPath = $"{tenantPath}/account/login";
        var returnUrl = $"{tenantPath}/foo";

        using var loginPage = await client.GetAsync(
            $"{loginPath}?ReturnUrl={Uri.EscapeDataString(returnUrl)}"
        );
        Assert.Equal(HttpStatusCode.OK, loginPage.StatusCode);
        var requestToken = ReadSetCookieValue(loginPage, AntiforgeryRequestCookieName);
        var cookieToken = ReadSetCookie(loginPage, AntiforgeryCookiePrefix)?.Split(';')[0];
        Assert.NotNull(requestToken);
        Assert.NotNull(cookieToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, loginPath);
        request.Headers.Add("Cookie", cookieToken);
        request.Content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["username"] = UserName,
                ["password"] = Password,
                ["ReturnUrl"] = returnUrl,
                [AntiforgeryFieldName] = requestToken,
            }
        );
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> GetWithCookieAsync(
        HttpClient client,
        string path,
        string cookie
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Add("Cookie", cookie);
        return await client.SendAsync(request);
    }

    private static string? ReadSetCookie(HttpResponseMessage response, string namePrefix) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.FirstOrDefault(cookie =>
                cookie.StartsWith(namePrefix, StringComparison.Ordinal)
            )
            : null;

    private static string? ReadSetCookieValue(HttpResponseMessage response, string name)
    {
        var firstPart = ReadSetCookie(response, $"{name}=")?.Split(';')[0];
        return firstPart is null ? null : Uri.UnescapeDataString(firstPart[(name.Length + 1)..]);
    }

    private static async Task SeedTenantAsync(
        HostApplicationFactory factory,
        string tenantId,
        IReadOnlyDictionary<string, object>? settings = null
    )
    {
        using var scope = factory.Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );

        await storeManager
            .GetStore<ITenantStore>()
            .AddAsync(
                new PersistedTenant
                {
                    TenantId = tenantId,
                    Issuer = $"http://localhost/{tenantId}",
                    ConcurrencyToken = Guid.NewGuid().ToString("N"),
                    IsDisabled = false,
                    DisplayName = tenantId,
                    Settings = new PersistedTenantSettings
                    {
                        TenantId = tenantId,
                        ConcurrencyToken = Guid.NewGuid().ToString("N"),
                        Value = settings is null
                            ? JsonSerializer.SerializeToElement(null, typeof(object))
                            : JsonSerializer.SerializeToElement(settings),
                    },
                    Secrets = new PersistedTenantSecrets
                    {
                        TenantId = tenantId,
                        ConcurrencyToken = Guid.NewGuid().ToString("N"),
                        Value = [],
                    },
                },
                CancellationToken.None
            );
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }

    private static async Task SeedLocalAccountAsync(HostApplicationFactory factory, string tenantId)
    {
        using var scope = factory.Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );

        // The store hashes the credential and generates the security stamp internally.
        await storeManager
            .GetStore<ILocalAccountStore>()
            .AddAsync(
                new PersistedLocalAccount
                {
                    TenantId = tenantId,
                    LocalAccountId = Guid.NewGuid().ToString("N"),
                    UserName = UserName,
                    Email = null,
                    EmailVerified = false,
                    IsEnabled = true,
                    Claims = [],
                    ConcurrencyToken = string.Empty,
                },
                SecureEncoding.UTF8.GetBytes(Password),
                CancellationToken.None
            );
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }
}
