// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Playwright;
using NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.Bff.EndToEnd.Tests.Blazor;

[Collection(EndToEndCollection.Name)]
public sealed class BlazorCircuitTests(EndToEndEnvironment environment)
{
    private const float WaitTimeoutMilliseconds = 30_000;

    private const string CircuitInteractiveScript =
        "() => document.getElementById('circuit-state')?.textContent === 'interactive'";

    private const string CircuitSignedOutScript =
        "() => document.getElementById('circuit-user')?.textContent === 'signed out'";

    private static string PingsScript(int count) =>
        $"() => document.getElementById('circuit-pings')?.textContent === '{count}'";

    #region Circuit Tests

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task Circuit_AfterBackChannelLogout_SignsOut(string browserName)
    {
        var bff = environment.BlazorBff;
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak);
        var before = bff.Counters.HubRequests;

        await page.GotoAsync($"{bff.Origin}/circuit");
        await page.WaitForFunctionAsync(
            CircuitInteractiveScript,
            null,
            new PageWaitForFunctionOptions { Timeout = WaitTimeoutMilliseconds }
        );

        Assert.Equal(KeycloakProvider.UserEmail, await page.InnerTextAsync("#circuit-user"));
        Assert.True(bff.Counters.HubRequests > before);

        await environment.Keycloak.LogOutUserAsync();

        await page.WaitForFunctionAsync(
            CircuitSignedOutScript,
            null,
            new PageWaitForFunctionOptions { Timeout = WaitTimeoutMilliseconds }
        );
        Assert.Equal("signed out", await page.InnerTextAsync("#circuit-user"));
    }

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task Circuit_InvocationAfterSessionEnded_StaysConnectedAndSignedOut(
        string browserName
    )
    {
        var bff = environment.BlazorBff;
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff, environment.Keycloak);

        await page.GotoAsync($"{bff.Origin}/circuit");
        await page.WaitForFunctionAsync(
            CircuitInteractiveScript,
            null,
            new PageWaitForFunctionOptions { Timeout = WaitTimeoutMilliseconds }
        );

        await page.ClickAsync("#circuit-ping");
        await page.WaitForFunctionAsync(
            PingsScript(1),
            null,
            new PageWaitForFunctionOptions { Timeout = WaitTimeoutMilliseconds }
        );

        await environment.Keycloak.LogOutUserAsync();

        await page.WaitForFunctionAsync(
            CircuitSignedOutScript,
            null,
            new PageWaitForFunctionOptions { Timeout = WaitTimeoutMilliseconds }
        );

        // The circuit's sign-out ended the connection session, so this invocation runs without it.
        await page.ClickAsync("#circuit-ping");
        await page.WaitForFunctionAsync(
            PingsScript(2),
            null,
            new PageWaitForFunctionOptions { Timeout = WaitTimeoutMilliseconds }
        );

        Assert.Equal("signed out", await page.InnerTextAsync("#circuit-user"));
        Assert.Equal("interactive", await page.InnerTextAsync("#circuit-state"));
    }

    #endregion
}
