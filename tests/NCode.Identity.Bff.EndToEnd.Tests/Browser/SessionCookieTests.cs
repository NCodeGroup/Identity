// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
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
        await using var context = await environment.NewContextAsync(
            browserName,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();

        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak, CancellationToken.None);

        var cookies = await context.CookiesAsync([bff.Origin]);
        var cookie = Assert.Single(cookies, x => x.Name == BffApplication.SessionCookieName);
        Assert.Equal("localhost", cookie.Domain);
        Assert.Equal("/", cookie.Path);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(SameSiteAttribute.Strict, cookie.SameSite);
        Assert.True(cookie.Expires > TimeProvider.System.GetUtcNow().ToUnixTimeSeconds());
        Assert.DoesNotContain(cookies, x => x.Name == BffApplication.LoginCookieName);

        var visibleCookies = await page.EvaluateAsync<string>("() => document.cookie");
        Assert.DoesNotContain(BffApplication.SessionCookieName, visibleCookies);
    }

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task Login_WithLaxSession_SetsLaxCookie(string browserName)
    {
        var bff = environment.LaxBff;
        await using var context = await environment.NewContextAsync(
            browserName,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();

        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak, CancellationToken.None);

        var cookies = await context.CookiesAsync([bff.Origin]);
        var cookie = Assert.Single(cookies, x => x.Name == BffApplication.SessionCookieName);
        Assert.Equal(SameSiteAttribute.Lax, cookie.SameSite);
    }

    #endregion

    #region UserEndpoint Tests

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task UserEndpoint_SameOriginFetchWithHeader_ReturnsTheUser(string browserName)
    {
        await using var context = await environment.NewContextAsync(
            browserName,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(
            page,
            environment.StrictBff,
            environment.Keycloak,
            CancellationToken.None
        );

        var outcome = await BrowserFlows.FetchAsync(
            page,
            "/bff/user",
            true,
            CancellationToken.None
        );

        Assert.False(outcome.Blocked);
        Assert.Equal(StatusCodes.Status200OK, outcome.Status);
        Assert.Contains(KeycloakProvider.UserEmail, outcome.Body);
    }

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task UserEndpoint_SameOriginFetchWithoutHeader_IsRefused(string browserName)
    {
        await using var context = await environment.NewContextAsync(
            browserName,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(
            page,
            environment.StrictBff,
            environment.Keycloak,
            CancellationToken.None
        );

        var outcome = await BrowserFlows.FetchAsync(
            page,
            "/bff/user",
            false,
            CancellationToken.None
        );

        Assert.False(outcome.Blocked);
        Assert.Equal(StatusCodes.Status403Forbidden, outcome.Status);
        Assert.Contains(BffProblemTypes.AntiforgeryHeaderMissing, outcome.Body);
    }

    #endregion
}
