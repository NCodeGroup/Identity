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
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Exercises an accepted <c>redirect_uri</c> end to end: the authorization response is delivered to the URI exactly as
/// the client registered it, and the token endpoint compares the URI ordinally (RFC 6749 §4.1.3).
/// </summary>
public class RedirectUriAsWrittenTests
{
    private const string UserName = "as-written-user";
    private const string Password = "as-written-password";
    private const string PrivateUseRedirectUri = "com.example.app://callback";
    private const string WebRedirectUri = "https://client.example/callback";

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
        string? sessionCookie,
        string clientId,
        string redirectUri,
        string extraQuery
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + TestPkce.QueryParameters
            + extraQuery;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth2/authorize" + query);
        if (sessionCookie is not null)
        {
            request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        }

        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> ContinueAsync(
        HttpClient client,
        string sessionCookie,
        string clientId,
        string redirectUri
    )
    {
        using var authorizeResponse = await AuthorizeAsync(
            client,
            sessionCookie: null,
            clientId,
            redirectUri,
            extraQuery: string.Empty
        );

        Assert.Equal(HttpStatusCode.Found, authorizeResponse.StatusCode);
        Assert.NotNull(client.BaseAddress);
        Assert.NotNull(authorizeResponse.Headers.Location);
        var loginLocation = new Uri(client.BaseAddress, authorizeResponse.Headers.Location);
        var continueUrl = HttpUtility.ParseQueryString(loginLocation.Query)["ReturnUrl"];
        Assert.False(string.IsNullOrEmpty(continueUrl));

        using var request = new HttpRequestMessage(HttpMethod.Get, continueUrl);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        var continueResponse = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.SeeOther, continueResponse.StatusCode);
        return continueResponse;
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Body)> RedeemAsync(
        HttpClient client,
        string clientId,
        string code,
        string redirectUri
    )
    {
        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = TestPkce.CodeVerifier,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token");
        request.Content = new FormUrlEncodedContent(form);

        using var response = await client.SendAsync(request);
        var body = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        return (response.StatusCode, body);
    }

    #endregion

    #region Redirect URI Tests

    [Fact]
    public async Task Authorize_WithAPrivateUseRedirectUri_RedirectsToTheUriAsWritten()
    {
        const string clientId = "it-as-written-direct";
        using var factory = new HostApplicationFactory();
        await factory.SeedPublicClientAsync(clientId, PrivateUseRedirectUri);
        var client = CreateClient(factory);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie: null,
            clientId,
            PrivateUseRedirectUri,
            extraQuery: "&prompt=none"
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(
            "com.example.app://callback?",
            location.OriginalString,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task Continue_WithAPrivateUseRedirectUri_DeliversToTheUriAsWrittenAndRedeemsWithIt()
    {
        const string clientId = "it-as-written-continue";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(clientId, PrivateUseRedirectUri);
        var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var response = await ContinueAsync(
            client,
            sessionCookie,
            clientId,
            PrivateUseRedirectUri
        );

        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(
            "com.example.app://callback?",
            location.OriginalString,
            StringComparison.Ordinal
        );
        var code = HttpUtility.ParseQueryString(location.Query)["code"];
        Assert.True(!string.IsNullOrEmpty(code), location.OriginalString);

        var (statusCode, body) = await RedeemAsync(client, clientId, code, PrivateUseRedirectUri);

        Assert.True(statusCode == HttpStatusCode.OK, body.ToString());
    }

    [Fact]
    public async Task Token_WhenTheRedirectUriIsItsNormalizedForm_ReturnsInvalidGrant()
    {
        const string clientId = "it-as-written-normalized";
        const string normalizedRedirectUri = "com.example.app://callback/";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(clientId, PrivateUseRedirectUri);
        var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var response = await ContinueAsync(
            client,
            sessionCookie,
            clientId,
            PrivateUseRedirectUri
        );
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var code = HttpUtility.ParseQueryString(location.Query)["code"];
        Assert.True(!string.IsNullOrEmpty(code), location.OriginalString);

        var (statusCode, body) = await RedeemAsync(client, clientId, code, normalizedRedirectUri);

        Assert.True(statusCode == HttpStatusCode.BadRequest, body.ToString());
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Token_WhenTheRedirectUriDiffersOnlyInHostCase_ReturnsInvalidGrant()
    {
        const string clientId = "it-as-written-host-case";
        const string hostCaseRedirectUri = "https://CLIENT.example/callback";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(clientId, WebRedirectUri);
        var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            WebRedirectUri,
            extraQuery: "&prompt=none"
        );
        var location = response.Headers.Location;
        Assert.NotNull(location);
        var code = HttpUtility.ParseQueryString(location.Query)["code"];
        Assert.True(!string.IsNullOrEmpty(code), location.OriginalString);

        var (statusCode, body) = await RedeemAsync(client, clientId, code, hostCaseRedirectUri);

        Assert.True(statusCode == HttpStatusCode.BadRequest, body.ToString());
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    #endregion
}
