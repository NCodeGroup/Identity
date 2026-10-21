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

using System.Buffers.Text;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Exercises JWT-secured authorization responses (JARM) end to end: the response parameters travel as one signed JWT
/// issued by the tenant and addressed to the client, in the <c>response</c> parameter of the requested response mode
/// (ADR-0201).
/// </summary>
public class JwtSecuredAuthorizationResponseTests
{
    private const string UserName = "jarm-user";
    private const string Password = "jarm-password";
    private const string ClientId = "it-jarm-public";
    private const string RedirectUri = "https://client.example/callback";

    #region Scaffolding

    private static async Task<(HttpClient Client, string SessionCookie)> SignInAsync(
        HostApplicationFactory factory
    )
    {
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(ClientId, RedirectUri);
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);
        return (client, sessionCookie);
    }

    private static async Task<HttpResponseMessage> AuthorizeAsync(
        HttpClient client,
        string? sessionCookie,
        string responseMode
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + "&prompt=none"
            + $"&response_mode={Uri.EscapeDataString(responseMode)}"
            + TestPkce.QueryParameters;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth2/authorize" + query);
        if (sessionCookie is not null)
            request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        return await client.SendAsync(request);
    }

    private static (JsonElement Header, JsonElement Payload) DecodeJwt(string? jwt)
    {
        Assert.False(string.IsNullOrEmpty(jwt));
        var segments = jwt.Split('.');
        Assert.Equal(3, segments.Length);
        var header = JsonElement.Parse(
            Encoding.UTF8.GetString(Base64Url.DecodeFromChars(segments[0]))
        );
        var payload = JsonElement.Parse(
            Encoding.UTF8.GetString(Base64Url.DecodeFromChars(segments[1]))
        );
        return (header, payload);
    }

    private static void AssertAddressedToTheClient(JsonElement header, JsonElement payload)
    {
        Assert.Equal("RS256", header.GetProperty("alg").GetString());
        Assert.Equal(ClientId, payload.GetProperty("aud").GetString());
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("iss").GetString()));
        var lifetime =
            payload.GetProperty("exp").GetInt64() - payload.GetProperty("iat").GetInt64();
        Assert.InRange(lifetime, 1, 600);
    }

    #endregion

    #region Response Mode Tests

    [Fact]
    public async Task Authorize_WithQueryJwt_ReturnsTheResponseAsASignedJwtInTheQuery()
    {
        using var factory = new HostApplicationFactory();
        var (client, sessionCookie) = await SignInAsync(factory);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            OpenIdConstants.ResponseModes.QueryJwt
        );

        Assert.Equal(HttpStatusCode.SeeOther, response.StatusCode);
        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.AbsoluteUri, StringComparison.Ordinal);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Null(parameters["code"]);
        Assert.Null(parameters["state"]);

        var (header, payload) = DecodeJwt(parameters["response"]);
        AssertAddressedToTheClient(header, payload);
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("code").GetString()));
        Assert.Equal("abc123", payload.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Authorize_WithTheJwtShorthandForACode_ResolvesToQueryJwt()
    {
        using var factory = new HostApplicationFactory();
        var (client, sessionCookie) = await SignInAsync(factory);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            OpenIdConstants.ResponseModes.Jwt
        );

        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        var (header, payload) = DecodeJwt(parameters["response"]);
        AssertAddressedToTheClient(header, payload);
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("code").GetString()));
    }

    [Fact]
    public async Task Authorize_WithFragmentJwt_ReturnsTheResponseJwtInTheFragment()
    {
        using var factory = new HostApplicationFactory();
        var (client, sessionCookie) = await SignInAsync(factory);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            OpenIdConstants.ResponseModes.FragmentJwt
        );

        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Fragment.TrimStart('#'));
        var (header, payload) = DecodeJwt(parameters["response"]);
        AssertAddressedToTheClient(header, payload);
        Assert.Equal("abc123", payload.GetProperty("state").GetString());
    }

    [Fact]
    public async Task Authorize_WithFormPostJwt_PostsTheResponseJwtAsTheOnlyField()
    {
        using var factory = new HostApplicationFactory();
        var (client, sessionCookie) = await SignInAsync(factory);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            OpenIdConstants.ResponseModes.FormPostJwt
        );

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("name='response'", html, StringComparison.Ordinal);
        Assert.DoesNotContain("name='code'", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authorize_WhenTheRequestFailsWithQueryJwt_ReturnsTheErrorAsASignedJwt()
    {
        using var factory = new HostApplicationFactory();
        var (client, _) = await SignInAsync(factory);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie: null,
            OpenIdConstants.ResponseModes.QueryJwt
        );

        var location = response.Headers.Location;
        Assert.NotNull(location);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Null(parameters["error"]);
        var (header, payload) = DecodeJwt(parameters["response"]);
        AssertAddressedToTheClient(header, payload);
        Assert.Equal(
            OpenIdConstants.ErrorCodes.LoginRequired,
            payload.GetProperty("error").GetString()
        );
        Assert.Equal("abc123", payload.GetProperty("state").GetString());
    }

    #endregion
}
