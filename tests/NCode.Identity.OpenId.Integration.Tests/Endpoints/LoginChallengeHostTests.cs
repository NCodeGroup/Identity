// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// The login redirect of an authorization request without a session is root-relative, so the request's
/// <c>Host</c> cannot choose where it points.
/// </summary>
// See ADR-1365.
public class LoginChallengeHostTests
{
    private const string ClientId = "it-login-host";
    private const string RedirectUri = "https://client.example/callback";

    [Fact]
    public async Task Authorize_WhenTheHostHeaderIsForged_RedirectsToTheLoginPageOnTheSameOrigin()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedPublicClientAsync(ClientId, RedirectUri);
        using var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var query =
            $"?client_id={Uri.EscapeDataString(ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + TestPkce.QueryParameters;
        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth2/authorize" + query);
        request.Headers.Host = "attacker.example";

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = response.Headers.Location?.OriginalString;
        Assert.NotNull(location);
        Assert.StartsWith("/account/login?ReturnUrl=", location, StringComparison.Ordinal);
        Assert.DoesNotContain("attacker.example", location, StringComparison.Ordinal);
    }
}
