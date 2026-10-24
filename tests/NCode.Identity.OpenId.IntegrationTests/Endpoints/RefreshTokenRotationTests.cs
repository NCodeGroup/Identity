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
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

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

    private static async Task<HttpResponseMessage> AuthorizeAsync(
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
        return await client.SendAsync(request);
    }

    private static async Task<string> GetCodeAsync(
        HttpClient client,
        string sessionCookie,
        string clientId
    )
    {
        using var response = await AuthorizeAsync(client, sessionCookie, clientId);

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

    private static Task<(HttpStatusCode StatusCode, JsonElement Body)> RedeemAsync(
        HttpClient client,
        string clientId,
        string code
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
            }
        );

    private static Task<(HttpStatusCode StatusCode, JsonElement Body)> RefreshAsync(
        HttpClient client,
        string clientId,
        string refreshToken
    ) =>
        PostTokenAsync(
            client,
            new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = clientId,
                ["refresh_token"] = refreshToken,
            }
        );

    private static string GetRefreshToken(HttpStatusCode statusCode, JsonElement body)
    {
        Assert.True(statusCode == HttpStatusCode.OK, body.ToString());

        var refreshToken = body.GetProperty("refresh_token").GetString();
        Assert.False(string.IsNullOrEmpty(refreshToken), body.ToString());
        return refreshToken;
    }

    // Signs in, authorizes and redeems the code for the first refresh token of a new rotation family.
    private static async Task<string> IssueRefreshTokenAsync(HttpClient client, string clientId)
    {
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        var code = await GetCodeAsync(client, sessionCookie, clientId);
        var (statusCode, body) = await RedeemAsync(client, clientId, code);
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
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(clientId, RedirectUri);
        var client = CreateClient(factory);

        var first = await IssueRefreshTokenAsync(client, clientId);

        var (rotatedStatus, rotatedBody) = await RefreshAsync(client, clientId, first);
        var second = GetRefreshToken(rotatedStatus, rotatedBody);
        Assert.NotEqual(first, second);

        var (reuseStatus, reuseBody) = await RefreshAsync(client, clientId, first);
        AssertInvalidGrant(reuseStatus, reuseBody);

        // The reuse revoked the rotation family, so the successor is no longer valid either.
        var (successorStatus, successorBody) = await RefreshAsync(client, clientId, second);
        AssertInvalidGrant(successorStatus, successorBody);
    }

    #endregion

    #region Reuse Interval Tests

    [Fact]
    public async Task Refresh_WithinTheReuseInterval_IssuesAnotherSuccessorAndKeepsTheFirst()
    {
        const string clientId = "it-rotation-reuse-interval";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientWithSettingsAsync(
            clientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.RefreshTokenReuseInterval] = "00:01:00",
            },
            RedirectUri
        );
        var client = CreateClient(factory);

        var original = await IssueRefreshTokenAsync(client, clientId);

        var (firstStatus, firstBody) = await RefreshAsync(client, clientId, original);
        var first = GetRefreshToken(firstStatus, firstBody);

        var (retryStatus, retryBody) = await RefreshAsync(client, clientId, original);
        var second = GetRefreshToken(retryStatus, retryBody);
        Assert.NotEqual(first, second);

        // The retry branched the rotation family: the first successor is still valid.
        var (successorStatus, successorBody) = await RefreshAsync(client, clientId, first);
        GetRefreshToken(successorStatus, successorBody);
    }

    #endregion
}
