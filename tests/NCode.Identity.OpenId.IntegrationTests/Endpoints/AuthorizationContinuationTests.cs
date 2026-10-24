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
/// Exercises the authorization continuation end to end: a request without a session is sent to the login page, and
/// once the user has signed in, the continue URL issues the code and delivers the response in the requested response
/// mode (ADR-0201).
/// </summary>
public class AuthorizationContinuationTests
{
    private const string UserName = "continue-user";
    private const string Password = "continue-password";
    private const string ClientId = "it-continue-public";
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

    private static async Task<HttpResponseMessage> ContinueAsync(
        HttpClient client,
        string sessionCookie,
        string responseMode
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(ClientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + $"&response_mode={Uri.EscapeDataString(responseMode)}"
            + TestPkce.QueryParameters;

        using var authorizeResponse = await client.GetAsync("/oauth2/authorize" + query);

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

    #endregion

    #region Continue Tests

    [Fact]
    public async Task Continue_AfterTheUserSignsIn_DeliversTheCodeAndStateInTheRequestedResponseMode()
    {
        using var factory = new HostApplicationFactory();
        var (client, sessionCookie) = await SignInAsync(factory);

        using var response = await ContinueAsync(
            client,
            sessionCookie,
            OpenIdConstants.ResponseModes.Fragment
        );

        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.AbsoluteUri, StringComparison.Ordinal);
        var parameters = HttpUtility.ParseQueryString(location.Fragment.TrimStart('#'));
        Assert.False(string.IsNullOrEmpty(parameters["code"]));
        Assert.Equal("abc123", parameters["state"]);
    }

    [Fact]
    public async Task Continue_AfterTheUserSignsIn_DeliversAJwtSecuredResponse()
    {
        using var factory = new HostApplicationFactory();
        var (client, sessionCookie) = await SignInAsync(factory);

        using var response = await ContinueAsync(
            client,
            sessionCookie,
            OpenIdConstants.ResponseModes.QueryJwt
        );

        var location = response.Headers.Location;
        Assert.NotNull(location);
        Assert.StartsWith(RedirectUri, location.AbsoluteUri, StringComparison.Ordinal);
        var parameters = HttpUtility.ParseQueryString(location.Query);
        Assert.Null(parameters["code"]);

        var (_, payload) = DecodeJwt(parameters["response"]);
        Assert.False(string.IsNullOrEmpty(payload.GetProperty("code").GetString()));
        Assert.Equal("abc123", payload.GetProperty("state").GetString());
    }

    #endregion
}
