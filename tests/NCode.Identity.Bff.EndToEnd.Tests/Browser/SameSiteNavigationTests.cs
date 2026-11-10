// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.Bff.EndToEnd.Tests.Browser;

[Collection(EndToEndCollection.Name)]
public sealed class SameSiteNavigationTests(EndToEndEnvironment environment)
{
    private sealed record UserResponse(string LogoutSid);

    #region Navigation Tests

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task Navigation_CrossSiteToStrictBff_CarriesNoSessionCookie(string browserName)
    {
        var bff = environment.StrictBff;
        await using var context = await environment.NewContextAsync(
            browserName,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak, CancellationToken.None);
        var originPage = await BrowserFlows.OpenOriginAsync(
            context,
            environment.CrossSiteDenied,
            CancellationToken.None
        );

        var text = await BrowserFlows.FollowLinkAsync(
            originPage,
            $"{bff.Origin}/probe/cookie",
            CancellationToken.None
        );

        Assert.Equal("absent", text);
    }

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task Navigation_CrossSiteToLaxBff_CarriesTheSessionCookie(string browserName)
    {
        var bff = environment.LaxBff;
        await using var context = await environment.NewContextAsync(
            browserName,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak, CancellationToken.None);
        var originPage = await BrowserFlows.OpenOriginAsync(
            context,
            environment.CrossSiteDenied,
            CancellationToken.None
        );

        var text = await BrowserFlows.FollowLinkAsync(
            originPage,
            $"{bff.Origin}/probe/cookie",
            CancellationToken.None
        );

        Assert.Equal("present", text);
    }

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task Navigation_SameSiteToStrictBff_CarriesTheSessionCookie(string browserName)
    {
        var bff = environment.StrictBff;
        await using var context = await environment.NewContextAsync(
            browserName,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak, CancellationToken.None);
        var originPage = await BrowserFlows.OpenOriginAsync(
            context,
            environment.SameSiteUntrusted,
            CancellationToken.None
        );

        var text = await BrowserFlows.FollowLinkAsync(
            originPage,
            $"{bff.Origin}/probe/cookie",
            CancellationToken.None
        );

        Assert.Equal("present", text);
    }

    #endregion

    #region FormPost Tests

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task FormPost_CrossSiteToApi_NeverRunsTheApi(string browserName)
    {
        var bff = environment.StrictBff;
        await using var context = await environment.NewContextAsync(
            browserName,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak, CancellationToken.None);
        var originPage = await BrowserFlows.OpenOriginAsync(
            context,
            environment.CrossSiteDenied,
            CancellationToken.None
        );
        var apiCalls = bff.ApiCalls;

        await BrowserFlows.SubmitFormAsync(
            originPage,
            $"{bff.Origin}/api/me",
            new Dictionary<string, string>(),
            CancellationToken.None
        );

        Assert.Equal(apiCalls, bff.ApiCalls);
        var outcome = await BrowserFlows.FetchAsync(
            page,
            "/bff/user",
            true,
            CancellationToken.None
        );
        Assert.Equal(StatusCodes.Status200OK, outcome.Status);
    }

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task FormPost_CrossSiteToLogout_KeepsTheSession(string browserName)
    {
        var bff = environment.StrictBff;
        await using var context = await environment.NewContextAsync(
            browserName,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak, CancellationToken.None);
        var user = await BrowserFlows.FetchAsync(page, "/bff/user", true, CancellationToken.None);
        var response = JsonSerializer.Deserialize<UserResponse>(
            user.Body,
            JsonSerializerOptions.Web
        );
        Assert.NotNull(response);
        var originPage = await BrowserFlows.OpenOriginAsync(
            context,
            environment.CrossSiteDenied,
            CancellationToken.None
        );

        await BrowserFlows.SubmitFormAsync(
            originPage,
            $"{bff.Origin}/bff/logout",
            new Dictionary<string, string> { ["sid"] = response.LogoutSid },
            CancellationToken.None
        );

        var outcome = await BrowserFlows.FetchAsync(
            page,
            "/bff/user",
            true,
            CancellationToken.None
        );
        Assert.Equal(StatusCodes.Status200OK, outcome.Status);
    }

    #endregion
}
