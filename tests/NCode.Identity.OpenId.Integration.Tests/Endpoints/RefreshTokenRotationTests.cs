// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Exercises refresh-token rotation end to end — an interactive authorization that asks for <c>offline_access</c>, the
/// refresh token its code redeems for, and the refresh requests that follow: a public client's unbound refresh token is
/// rotated and its reuse revokes the rotation family, and a client that opts in to a reuse interval can retry a refresh
/// with the token it already spent.
/// </summary>
public class RefreshTokenRotationTests
{
    private const string UserName = "rotation-user";
    private const string Password = "rotation-password";
    private const string RedirectUri = "https://client.example/callback";

    #region Scaffolding

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );

    private static async ValueTask<HttpResponseMessage> AuthorizeAsync(
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
        return await client.SendAsync(request, cancellationToken);
    }

    private static async ValueTask<string> GetCodeAsync(
        HttpClient client,
        string sessionCookie,
        string clientId,
        CancellationToken cancellationToken
    )
    {
        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            cancellationToken
        );

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
        var body = JsonElement.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        return (response.StatusCode, body);
    }

    private static ValueTask<(HttpStatusCode StatusCode, JsonElement Body)> RedeemAsync(
        HttpClient client,
        string clientId,
        string code,
        CancellationToken cancellationToken
    ) =>
        PostTokenAsync(
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

    private static ValueTask<(HttpStatusCode StatusCode, JsonElement Body)> RefreshAsync(
        HttpClient client,
        string clientId,
        string refreshToken,
        CancellationToken cancellationToken
    ) =>
        PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken,
            },
            cancellationToken
        );

    private static string GetRefreshToken(HttpStatusCode statusCode, JsonElement body)
    {
        Assert.True(statusCode == HttpStatusCode.OK, body.ToString());

        var refreshToken = body.GetProperty("refresh_token").GetString();
        Assert.False(string.IsNullOrEmpty(refreshToken), body.ToString());
        return refreshToken;
    }

    // Signs in, authorizes and redeems the code for the first refresh token of a new rotation family.
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
        var (statusCode, body) = await RedeemAsync(client, clientId, code, cancellationToken);
        return GetRefreshToken(statusCode, body);
    }

    private static void AssertInvalidGrant(HttpStatusCode statusCode, JsonElement body)
    {
        Assert.Equal(HttpStatusCode.BadRequest, statusCode);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    #endregion

    #region Rotation Tests

    [Fact]
    public async Task Refresh_ForAPublicClientWithoutAProofOfPossession_RotatesTheRefreshToken()
    {
        const string clientId = "it-rotation-public";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);

        var first = await IssueRefreshTokenAsync(client, clientId, CancellationToken.None);

        var (rotatedStatus, rotatedBody) = await RefreshAsync(
            client,
            clientId,
            first,
            CancellationToken.None
        );
        var second = GetRefreshToken(rotatedStatus, rotatedBody);
        Assert.NotEqual(first, second);

        var (reuseStatus, reuseBody) = await RefreshAsync(
            client,
            clientId,
            first,
            CancellationToken.None
        );
        AssertInvalidGrant(reuseStatus, reuseBody);

        // The reuse revoked the rotation family, so the successor is no longer valid either.
        var (successorStatus, successorBody) = await RefreshAsync(
            client,
            clientId,
            second,
            CancellationToken.None
        );
        AssertInvalidGrant(successorStatus, successorBody);
    }

    [Fact]
    public async Task Refresh_ForARotatedFamily_AnnouncesTheFamilysFixedHorizon()
    {
        const string clientId = "it-rotation-horizon";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await factory.SeedPublicClientAsync(clientId, [RedirectUri], CancellationToken.None);
        var client = CreateClient(factory);

        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            CancellationToken.None
        );
        var code = await GetCodeAsync(client, sessionCookie, clientId, CancellationToken.None);
        var (redeemStatus, redeemBody) = await RedeemAsync(
            client,
            clientId,
            code,
            CancellationToken.None
        );
        var first = GetRefreshToken(redeemStatus, redeemBody);
        var firstHorizon = redeemBody.GetProperty("refresh_token_expires_in").GetInt64();
        Assert.Equal((long)TimeSpan.FromDays(30).TotalSeconds, firstHorizon);

        // A rotation does not renew the family: its successor expires when the family does (ADR-1297).
        var (rotatedStatus, rotatedBody) = await RefreshAsync(
            client,
            clientId,
            first,
            CancellationToken.None
        );
        GetRefreshToken(rotatedStatus, rotatedBody);
        var rotatedHorizon = rotatedBody.GetProperty("refresh_token_expires_in").GetInt64();
        Assert.InRange(rotatedHorizon, 1, firstHorizon);
    }

    #endregion

    #region Reuse Interval Tests

    [Fact]
    public async Task Refresh_WithinTheReuseInterval_IssuesAnotherSuccessorAndKeepsTheFirst()
    {
        const string clientId = "it-rotation-reuse-interval";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password, CancellationToken.None);
        await factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.RefreshTokenReuseInterval] = "00:01:00",
            },
            [RedirectUri],
            CancellationToken.None
        );
        var client = CreateClient(factory);

        var original = await IssueRefreshTokenAsync(client, clientId, CancellationToken.None);

        var (firstStatus, firstBody) = await RefreshAsync(
            client,
            clientId,
            original,
            CancellationToken.None
        );
        var first = GetRefreshToken(firstStatus, firstBody);

        var (retryStatus, retryBody) = await RefreshAsync(
            client,
            clientId,
            original,
            CancellationToken.None
        );
        var second = GetRefreshToken(retryStatus, retryBody);
        Assert.NotEqual(first, second);

        // The retry branched the rotation family: the first successor is still valid.
        var (successorStatus, successorBody) = await RefreshAsync(
            client,
            clientId,
            first,
            CancellationToken.None
        );
        GetRefreshToken(successorStatus, successorBody);
    }

    #endregion
}
