// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.Bff.EndToEnd.Tests.Blazor;

[Collection(EndToEndCollection.Name)]
public sealed class HubConnectionTests(EndToEndEnvironment environment)
{
    #region BlazorHub Tests

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task BlazorHub_WebSocketFromAnotherOrigin_IsRefused(string browserName)
    {
        var bff = environment.BlazorBff;
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff.Origin, environment.Keycloak);
        var originPage = await BrowserFlows.OpenOriginAsync(context, environment.CrossSiteAdmitted);
        var before = bff.Counters.HubRequests;
        var url = $"{bff.WebSocketOrigin}/_blazor";

        var refused = await HubFlows.ProbeWebSocketAsync(originPage, url);
        var afterRefusal = bff.Counters.HubRequests;
        var admitted = await HubFlows.ProbeWebSocketAsync(page, url);

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
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff.Origin, environment.Keycloak);

        var opened = await HubFlows.OpenHubConnectionAsync(
            page,
            $"{bff.WebSocketOrigin}/hubs/notifications"
        );
        Assert.Equal("open", opened);

        await Task.Delay(TimeSpan.FromSeconds(5));
        Assert.False(await HubFlows.IsHubConnectionClosedAsync(page));

        await environment.Keycloak.LogOutUserAsync();

        await HubFlows.WaitForHubConnectionClosedAsync(page);
        Assert.True(await HubFlows.IsHubConnectionClosedAsync(page));
    }

    #endregion
}
