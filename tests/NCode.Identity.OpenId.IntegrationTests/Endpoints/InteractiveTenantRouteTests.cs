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
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Accounts.DataContracts;
using NCode.Identity.OpenId.Accounts.Stores;
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

    private static ConfiguredHostApplicationFactory CreatePathFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                    .TenantStrategyCodes
                    .DynamicByPath,
                ["OpenId:TenantResolution:DynamicByPath:IssuerTemplate"] =
                    "http://localhost/{tenantId}",
            }
        );

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

    private static async Task SeedTenantAsync(HostApplicationFactory factory, string tenantId)
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
                        Value = JsonSerializer.SerializeToElement(null, typeof(object)),
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
                Encoding.UTF8.GetBytes(Password),
                CancellationToken.None
            );
        await storeManager.SaveChangesAsync(CancellationToken.None);
    }
}
