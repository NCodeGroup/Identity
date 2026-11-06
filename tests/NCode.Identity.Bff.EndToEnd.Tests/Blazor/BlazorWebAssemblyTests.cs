// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using NCode.Identity.Bff.EndToEnd.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.Bff.EndToEnd.Tests.Blazor;

[Collection(EndToEndCollection.Name)]
public sealed partial class BlazorWebAssemblyTests(EndToEndEnvironment environment)
{
    private const float WaitTimeoutMilliseconds = 60_000;

    private const string ApiRenderedScript = "() => document.getElementById('wasm-api') !== null";

    private static readonly string[] BodyResourceTypes = ["document", "fetch", "xhr"];

    [GeneratedRegex(@"eyJ[A-Za-z0-9_-]+\.eyJ[A-Za-z0-9_-]+\.")]
    private static partial Regex Jwt();

    #region WebAssembly Tests

    [Theory, MemberData(nameof(BrowserNames.All), MemberType = typeof(BrowserNames))]
    public async Task WebAssembly_SignedIn_CarriesNoToken(string browserName)
    {
        var bff = environment.BlazorBff;
        await using var context = await environment.NewContextAsync(browserName);
        var page = await context.NewPageAsync();
        await BrowserFlows.LoginAsync(page, bff.Origin, environment.Keycloak);
        var requests = new ConcurrentQueue<IRequest>();
        page.Request += (_, request) => requests.Enqueue(request);
        var calls = bff.Counters.ApiCalls;
        var tokenHeaders = bff.Counters.TokenHeaderRequests;

        await page.GotoAsync($"{bff.Origin}/wasm");
        await page.WaitForFunctionAsync(
            ApiRenderedScript,
            null,
            new PageWaitForFunctionOptions { Timeout = WaitTimeoutMilliseconds }
        );

        Assert.Equal(KeycloakProvider.UserEmail, await page.InnerTextAsync("#wasm-user"));
        Assert.StartsWith("200:", await page.InnerTextAsync("#wasm-api"), StringComparison.Ordinal);
        Assert.True(bff.Counters.ApiCalls > calls);
        Assert.Equal(tokenHeaders, bff.Counters.TokenHeaderRequests);

        var user = await BrowserFlows.FetchAsync(page, "/bff/user", true);
        Assert.Equal(200, user.Status);
        Assert.Contains(KeycloakProvider.UserEmail, user.Body, StringComparison.Ordinal);
        Assert.DoesNotMatch(Jwt(), user.Body);

        foreach (var request in requests)
        {
            var headers = await request.AllHeadersAsync();
            Assert.False(headers.ContainsKey("authorization"), request.Url);
            Assert.False(headers.ContainsKey("dpop"), request.Url);

            if (
                !request.Url.StartsWith(bff.Origin, StringComparison.Ordinal)
                || request.Url.Contains("/_framework/", StringComparison.Ordinal)
                || !BodyResourceTypes.Contains(request.ResourceType)
            )
            {
                continue;
            }

            var response = await request.ResponseAsync();
            if (response is { Status: < 300 or >= 400 })
            {
                Assert.DoesNotMatch(Jwt(), await response.TextAsync());
            }
        }
    }

    #endregion
}
