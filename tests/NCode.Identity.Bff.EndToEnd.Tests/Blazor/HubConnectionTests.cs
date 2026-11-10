// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.Bff.EndToEnd.Tests.Blazor;

[Collection(EndToEndCollection.Name)]
public sealed class HubConnectionTests(EndToEndEnvironment environment)
{
    /// <summary>
    /// The number of revalidation intervals an idle connection stays open while its session is alive.
    /// </summary>
    private const int RevalidationsBeforeLogout = 3;

    #region BlazorHub Tests

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task BlazorHub_WebSocketFromAnotherOrigin_IsRefused(string browserName)
    {
        var bff = environment.BlazorBff;
        await using var context = await environment.NewContextAsync(
            browserName,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak, CancellationToken.None);
        var originPage = await BrowserFlows.OpenOriginAsync(
            context,
            environment.CrossSiteAdmitted,
            CancellationToken.None
        );
        var before = bff.Counters.HubRequests;
        var url = $"{bff.WebSocketOrigin}/_blazor";

        var refused = await HubFlows.ProbeWebSocketAsync(originPage, url, CancellationToken.None);
        var afterRefusal = bff.Counters.HubRequests;
        var admitted = await HubFlows.ProbeWebSocketAsync(page, url, CancellationToken.None);

        Assert.Equal("refused", refused);
        Assert.Equal(before, afterRefusal);
        Assert.Equal("open", admitted);
        Assert.True(bff.Counters.HubRequests > afterRefusal);
    }

    #endregion

    #region IdleHub Tests

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task IdleHub_AfterBackChannelLogout_ClosesTheConnection(string browserName)
    {
        var bff = environment.BlazorBff;
        await using var context = await environment.NewContextAsync(
            browserName,
            CancellationToken.None
        );
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak, CancellationToken.None);

        var opened = await HubFlows.OpenHubConnectionAsync(
            page,
            $"{bff.WebSocketOrigin}/hubs/notifications",
            CancellationToken.None
        );
        Assert.Equal("open", opened);

        await Task.Delay(bff.RevalidationInterval * RevalidationsBeforeLogout);
        Assert.False(await HubFlows.IsHubConnectionClosedAsync(page, CancellationToken.None));

        await environment.Keycloak.LogOutUserAsync(CancellationToken.None);

        await HubFlows.WaitForHubConnectionClosedAsync(page, CancellationToken.None);
        Assert.True(await HubFlows.IsHubConnectionClosedAsync(page, CancellationToken.None));
    }

    #endregion
}
