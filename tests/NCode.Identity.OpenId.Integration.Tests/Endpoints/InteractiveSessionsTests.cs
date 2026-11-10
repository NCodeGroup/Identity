// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// The active-sessions list reads the stored session and reports when it ends. See ADR-1220.
public class InteractiveSessionsTests
{
    private const string UserName = "admin";
    private const string Password = "dev-password";
    private const string SessionsListPath = "/account/sessions/list";

    [Fact]
    public async Task GetSessionsList_AfterSignIn_ListsTheSessionEndingAfterTheDefaultIdleTimeout()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

        var cookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );

        using var request = new HttpRequestMessage(HttpMethod.Get, SessionsListPath);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={cookie}");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        var session = Assert.Single(jsonElement.GetProperty("sessions").EnumerateArray());

        Assert.Equal(
            session.GetProperty("lastActivityWhen").GetInt64()
                + (long)TimeSpan.FromDays(3).TotalSeconds,
            session.GetProperty("expiresWhen").GetInt64()
        );
    }
}
