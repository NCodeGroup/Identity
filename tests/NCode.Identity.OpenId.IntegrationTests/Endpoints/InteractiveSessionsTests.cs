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
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// The active-sessions list reads the stored session and reports when it ends. See ADR-0220.
public class InteractiveSessionsTests
{
    private const string UserName = "admin";
    private const string Password = "dev-password";
    private const string SessionsListPath = "/account/sessions/list";

    [Fact]
    public async Task GetSessionsList_AfterSignIn_ListsTheSessionEndingAfterTheDefaultIdleTimeout()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

        var cookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var request = new HttpRequestMessage(HttpMethod.Get, SessionsListPath);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={cookie}");
        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var document = JsonElement.Parse(body);
        var session = Assert.Single(document.GetProperty("sessions").EnumerateArray());

        Assert.Equal(
            session.GetProperty("lastActivityWhen").GetInt64()
                + (long)TimeSpan.FromDays(3).TotalSeconds,
            session.GetProperty("expiresWhen").GetInt64()
        );
    }
}
