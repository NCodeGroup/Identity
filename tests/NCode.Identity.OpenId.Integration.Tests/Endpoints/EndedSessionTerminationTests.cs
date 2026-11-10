// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A session that ends while no request uses it is terminated in the background (ADR-1223).
public class EndedSessionTerminationTests
{
    private const string UserName = "ended-session-user";
    private const string Password = "ended-session-password";
    private const string RedirectUri = "https://client.example/callback";

    private static TimeSpan PollInterval { get; } = TimeSpan.FromMilliseconds(100);
    private static TimeSpan PollTimeout { get; } = TimeSpan.FromSeconds(15);

    #region Helpers

    private static async ValueTask<string> GetCodeAsync(
        HttpClient client,
        string sessionCookie,
        string clientId,
        CancellationToken cancellationToken
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
        using var response = await client.SendAsync(request, cancellationToken);

        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);

        var code = parameters["code"];
        Assert.True(!string.IsNullOrEmpty(code), $"error='{parameters["error"]}'");
        return code;
    }

    private static async ValueTask<(HttpStatusCode StatusCode, JsonElement Body)> PostTokenAsync(
        HttpClient client,
        Dictionary<string, string> form,
        CancellationToken cancellationToken
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
        {
            Content = new FormUrlEncodedContent(form),
        };

        using var response = await client.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        var body = JsonElement.Parse(responseBody);
        return (response.StatusCode, body);
    }

    // Signs in, authorizes and redeems the code for the refresh token of the new session.
    private static async ValueTask<string> IssueRefreshTokenAsync(
        HttpClient client,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            cancellationToken
        );

        var code = await GetCodeAsync(client, sessionCookie, clientId, cancellationToken);
        var (statusCode, body) = await PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = "authorization_code",
                ["client_id"] = clientId,
                ["code"] = code,
                ["redirect_uri"] = RedirectUri,
                ["code_verifier"] = TestPkce.CodeVerifier,
            },
            cancellationToken
        );
        Assert.True(statusCode == HttpStatusCode.OK, body.ToString());

        var refreshToken = body.GetProperty("refresh_token").GetString();
        Assert.False(string.IsNullOrEmpty(refreshToken), body.ToString());
        return refreshToken;
    }

    // Ends the only session by moving its expiration into the past, as if no request used it until it ended, and
    // returns the identifier of its grant.
    private static async ValueTask<string> EndTheSessionAsync(
        IServiceProvider services,
        CancellationToken cancellationToken
    )
    {
        using var scope = services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();

        var page = await store.GetPageAsync(
            new GrantFilter { GrantType = OpenIdConstants.PersistedGrantTypes.Session },
            new PageRequest { Limit = 10 },
            cancellationToken
        );
        var session = Assert.Single(page.Items);
        Assert.Null(session.RevokedWhen);

        session.ExpiresWhen = DateTimeOffset.UtcNow.AddMinutes(-1);
        await store.UpdateAsync(session, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);

        return session.GrantId;
    }

    private static async ValueTask<PersistedGrant?> FindAsync(
        IServiceProvider services,
        string grantId,
        CancellationToken cancellationToken
    )
    {
        using var scope = services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();
        return await store.GetOrDefaultAsync(grantId, cancellationToken);
    }

    #endregion

    #region Termination Tests

    [Fact]
    public async Task HostedService_WhenASessionEndsUnused_TerminatesItAndRevokesItsRefreshToken()
    {
        const string clientId = "it-ended-session";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        using var host = factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(serviceCollection =>
                serviceCollection.Configure<EndedSessionTerminationOptions>(options =>
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

        var refreshToken = await IssueRefreshTokenAsync(client, clientId, CancellationToken.None);

        var grantId = await EndTheSessionAsync(host.Services, CancellationToken.None);

        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < PollTimeout)
        {
            if (
                (await FindAsync(host.Services, grantId, CancellationToken.None))?.RevokedWhen
                is not null
            )
                break;

            await Task.Delay(PollInterval);
        }

        var session = await FindAsync(host.Services, grantId, CancellationToken.None);
        Assert.NotNull(session);
        Assert.NotNull(session.RevokedWhen);

        var (statusCode, body) = await PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken,
            },
            CancellationToken.None
        );
        Assert.Equal(HttpStatusCode.BadRequest, statusCode);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    #endregion
}
