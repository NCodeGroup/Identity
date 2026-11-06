// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.Bff.EndToEnd.Tests.Browser;

[Collection(EndToEndCollection.Name)]
public sealed class CorsAndFetchMetadataTests(EndToEndEnvironment environment)
{
    #region Cors Tests

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task Fetch_FromOriginCorsRefuses_DiesAtThePreflight(string browserName)
    {
        var bff = environment.StrictBff;
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak);
        var originPage = await BrowserFlows.OpenOriginAsync(context, environment.CrossSiteDenied);
        var apiCalls = bff.ApiCalls;
        var preflightRequests = bff.PreflightRequests;

        var outcome = await BrowserFlows.FetchAsync(originPage, $"{bff.Origin}/api/me", true);

        Assert.True(outcome.Blocked);
        Assert.True(bff.PreflightRequests > preflightRequests);
        Assert.Equal(apiCalls, bff.ApiCalls);
    }

    #endregion

    #region FetchMetadata Tests

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task Fetch_FromUntrustedSameSiteOrigin_IsRefusedAsCrossSite(string browserName)
    {
        var bff = environment.StrictBff;
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak);
        var originPage = await BrowserFlows.OpenOriginAsync(context, environment.SameSiteUntrusted);
        var apiCalls = bff.ApiCalls;

        var outcome = await BrowserFlows.FetchAsync(originPage, $"{bff.Origin}/api/me", true);

        Assert.Equal(StatusCodes.Status403Forbidden, outcome.Status);
        Assert.Contains(BffProblemTypes.CrossSiteRequest, outcome.Body);
        Assert.Equal(apiCalls, bff.ApiCalls);
    }

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task Fetch_FromCrossSiteOriginCorsAdmits_IsRefusedAsCrossSite(string browserName)
    {
        var bff = environment.StrictBff;
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak);
        var originPage = await BrowserFlows.OpenOriginAsync(context, environment.CrossSiteAdmitted);
        var apiCalls = bff.ApiCalls;

        var outcome = await BrowserFlows.FetchAsync(originPage, $"{bff.Origin}/api/me", true);

        Assert.Equal(StatusCodes.Status403Forbidden, outcome.Status);
        Assert.Contains(BffProblemTypes.CrossSiteRequest, outcome.Body);
        Assert.Equal(apiCalls, bff.ApiCalls);
    }

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task Fetch_FromTrustedSameSiteOrigin_ReachesTheApi(string browserName)
    {
        var bff = environment.StrictBff;
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak);
        var originPage = await BrowserFlows.OpenOriginAsync(context, environment.SameSiteTrusted);
        var apiCalls = bff.ApiCalls;

        var outcome = await BrowserFlows.FetchAsync(originPage, $"{bff.Origin}/api/me", true);

        Assert.Equal(StatusCodes.Status200OK, outcome.Status);
        Assert.Equal(apiCalls + 1, bff.ApiCalls);
    }

    #endregion
}
