// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;
using NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.Bff.EndToEnd.Tests.Browser;

[Collection(EndToEndCollection.Name)]
public sealed class SessionCookieTests(EndToEndEnvironment environment)
{
    #region Login Tests

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task Login_WithStrictSession_SetsHostOnlyHttpOnlySecureStrictCookie(
        string browserName
    )
    {
        var bff = environment.StrictBff;
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();

        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak);

        var cookies = await context.CookiesAsync([bff.Origin]);
        var cookie = Assert.Single(cookies, x => x.Name == BffDefaults.SessionCookieName);
        Assert.Equal("localhost", cookie.Domain);
        Assert.Equal("/", cookie.Path);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(SameSiteAttribute.Strict, cookie.SameSite);
        Assert.True(cookie.Expires > DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        Assert.DoesNotContain(cookies, x => x.Name == BffDefaults.LoginCookieName);

        var visibleCookies = await page.EvaluateAsync<string>("() => document.cookie");
        Assert.DoesNotContain(BffDefaults.SessionCookieName, visibleCookies);
    }

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task Login_WithLaxSession_SetsLaxCookie(string browserName)
    {
        var bff = environment.LaxBff;
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();

        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak);

        var cookies = await context.CookiesAsync([bff.Origin]);
        var cookie = Assert.Single(cookies, x => x.Name == BffDefaults.SessionCookieName);
        Assert.Equal(SameSiteAttribute.Lax, cookie.SameSite);
    }

    #endregion

    #region UserEndpoint Tests

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task UserEndpoint_SameOriginFetchWithHeader_ReturnsTheUser(string browserName)
    {
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, environment.StrictBff, environment.Keycloak);

        var outcome = await BrowserFlows.FetchAsync(page, "/bff/user", true);

        Assert.False(outcome.Blocked);
        Assert.Equal(200, outcome.Status);
        Assert.Contains(KeycloakProvider.UserEmail, outcome.Body);
    }

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task UserEndpoint_SameOriginFetchWithoutHeader_IsRefused(string browserName)
    {
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, environment.StrictBff, environment.Keycloak);

        var outcome = await BrowserFlows.FetchAsync(page, "/bff/user", false);

        Assert.False(outcome.Blocked);
        Assert.Equal(403, outcome.Status);
        Assert.Contains(BffProblemTypes.AntiforgeryHeaderMissing, outcome.Body);
    }

    #endregion
}
