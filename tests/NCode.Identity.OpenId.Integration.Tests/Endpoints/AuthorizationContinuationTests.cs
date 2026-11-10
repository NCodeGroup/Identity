// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

/// <summary>
/// Exercises the authorization continuation end to end: a request without a session is sent to the login page, and
/// once the user has signed in, the continue URL issues the code and delivers the response in the requested response
/// mode.
/// </summary>
// See ADR-1201.
public class AuthorizationContinuationTests
{
    private const string UserName = "continue-user";
    private const string Password = "continue-password";
    private const string ClientId = "it-continue-public";
    private const string RedirectUri = "https://client.example/callback";

    #region Scaffolding

    private static async ValueTask<(HttpClient Client, string SessionCookie)> SignInAsync(
        HostApplicationFactory factory,
        CancellationToken cancellationToken
    )
    {
        await factory.SeedLocalAccountAsync(UserName, Password, cancellationToken);
        await factory.SeedPublicClientAsync(ClientId, [RedirectUri], cancellationToken);
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                AllowAutoRedirect = false,
                HandleCookies = false,
            }
        );
        var sessionCookie = await InteractiveLogin.SignInAsync(
            client,
            UserName,
            Password,
            cancellationToken
        );
        return (client, sessionCookie);
    }

    private static async ValueTask<HttpResponseMessage> ContinueAsync(
        HttpClient client,
        string sessionCookie,
        string responseMode,
        CancellationToken cancellationToken
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

        using var authorizeResponse = await client.GetAsync(
            "/oauth2/authorize" + query,
            cancellationToken
        );

        Assert.Equal(HttpStatusCode.Found, authorizeResponse.StatusCode);
        Assert.NotNull(client.BaseAddress);
        Assert.NotNull(authorizeResponse.Headers.Location);
        var loginLocation = new Uri(client.BaseAddress, authorizeResponse.Headers.Location);
        var continueUrl = HttpUtility.ParseQueryString(loginLocation.Query)["ReturnUrl"];
        Assert.False(string.IsNullOrEmpty(continueUrl));

        using var request = new HttpRequestMessage(HttpMethod.Get, continueUrl);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        var continueResponse = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.SeeOther, continueResponse.StatusCode);
        return continueResponse;
    }

    private static (JsonElement Header, JsonElement Payload) DecodeJwt(string? jwt)
    {
        Assert.False(string.IsNullOrEmpty(jwt));
        var segments = jwt.Split('.');
        Assert.Equal(3, segments.Length);
        var header = JsonElement.Parse(
            SecureEncoding.UTF8.GetString(Base64Url.DecodeFromChars(segments[0]))
        );
        var payload = JsonElement.Parse(
            SecureEncoding.UTF8.GetString(Base64Url.DecodeFromChars(segments[1]))
        );
        return (header, payload);
    }

    #endregion

    #region Continue Tests

    [Fact]
    public async Task Continue_AfterTheUserSignsIn_DeliversTheCodeAndStateInTheRequestedResponseMode()
    {
        using var factory = new HostApplicationFactory();
        var (client, sessionCookie) = await SignInAsync(factory, CancellationToken.None);

        using var response = await ContinueAsync(
            client,
            sessionCookie,
            OpenIdProtocol.ResponseModes.Fragment,
            CancellationToken.None
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
        var (client, sessionCookie) = await SignInAsync(factory, CancellationToken.None);

        using var response = await ContinueAsync(
            client,
            sessionCookie,
            OpenIdProtocol.ResponseModes.QueryJwt,
            CancellationToken.None
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
