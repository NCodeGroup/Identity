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

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// A session that ends while no request uses it is terminated in the background (ADR-0223).
public class EndedSessionTerminationTests
{
    private const string UserName = "ended-session-user";
    private const string Password = "ended-session-password";
    private const string RedirectUri = "https://client.example/callback";

    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan PollTimeout = TimeSpan.FromSeconds(15);

    #region Helpers

    private static async Task<string> GetCodeAsync(
        HttpClient client,
        string sessionCookie,
        string clientId
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope="
            + Uri.EscapeDataString("openid offline_access")
            + "&state=abc123"
            + "&prompt=none"
            + TestPkce.QueryParameters;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth2/authorize" + query);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        using var response = await client.SendAsync(request);

        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);

        var code = parameters["code"];
        Assert.True(!string.IsNullOrEmpty(code), $"error='{parameters["error"]}'");
        return code;
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Body)> PostTokenAsync(
        HttpClient client,
        Dictionary<string, string> form
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
        {
            Content = new FormUrlEncodedContent(form),
        };

        using var response = await client.SendAsync(request);
        var body = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        return (response.StatusCode, body);
    }

    // Signs in, authorizes and redeems the code for the refresh token of the new session.
    private static async Task<string> IssueRefreshTokenAsync(HttpClient client, string clientId)
    {
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        var code = await GetCodeAsync(client, sessionCookie, clientId);
        var (statusCode, body) = await PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = TestPkce.CodeVerifier,
            }
        );
        Assert.True(statusCode == HttpStatusCode.OK, body.ToString());

        var refreshToken = body.GetProperty("refresh_token").GetString();
        Assert.False(string.IsNullOrEmpty(refreshToken), body.ToString());
        return refreshToken;
    }

    // Ends the only session by moving its expiration into the past, as if no request used it until it ended, and
    // returns the identifier of its grant.
    private static async Task<string> EndTheSessionAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<IGrantStore>();

        var page = await store.GetPageAsync(
            new GrantFilter { GrantType = OpenIdConstants.PersistedGrantTypes.Session },
            new PageRequest { Limit = 10 },
            CancellationToken.None
        );
        var session = Assert.Single(page.Items);
        Assert.Null(session.RevokedWhen);

        session.ExpiresWhen = DateTimeOffset.UtcNow.AddMinutes(-1);
        await store.UpdateAsync(session, CancellationToken.None);
        await storeManager.SaveChangesAsync(CancellationToken.None);

        return session.GrantId;
    }

    private static async Task<PersistedGrant?> FindAsync(IServiceProvider services, string grantId)
    {
        using var scope = services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var store = storeManager.GetStore<IGrantStore>();
        return await store.GetOrDefaultAsync(grantId, CancellationToken.None);
    }

    #endregion

    #region Termination Tests

    [Fact]
    public async Task HostedService_WhenASessionEndsUnused_TerminatesItAndRevokesItsRefreshToken()
    {
        const string clientId = "it-ended-session";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(clientId, RedirectUri);
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Configure<EndedSessionTerminationOptions>(options =>
                    options.Interval = TimeSpan.FromMilliseconds(50)
                )
            )
        );
        using var client = host.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

        var refreshToken = await IssueRefreshTokenAsync(client, clientId);

        var grantId = await EndTheSessionAsync(host.Services);

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < PollTimeout)
        {
            if ((await FindAsync(host.Services, grantId))?.RevokedWhen is not null)
                break;

            await Task.Delay(PollInterval);
        }

        var session = await FindAsync(host.Services, grantId);
        Assert.NotNull(session);
        Assert.NotNull(session.RevokedWhen);

        var (statusCode, body) = await PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken,
            }
        );
        Assert.Equal(HttpStatusCode.BadRequest, statusCode);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    #endregion
}
