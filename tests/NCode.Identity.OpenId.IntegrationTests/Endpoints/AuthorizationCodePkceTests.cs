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
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Exercises PKCE end to end — an interactive authorization, the code it issues, and the token request that redeems it:
/// every code is bound to an <c>S256</c> challenge by default, and only a deployment that relaxes <c>require_pkce</c> and
/// attests its clients with <c>pkce_nonce_exemption</c> lets a confidential OpenID Connect client rely on the nonce
/// instead.
/// </summary>
public class AuthorizationCodePkceTests
{
    private const string UserName = "pkce-user";
    private const string Password = "pkce-password";
    private const string RedirectUri = "https://client.example/callback";
    private const string Nonce = "n-0S6_WzA2Mj";

    private static readonly Dictionary<string, string?> NonceExemptionConfiguration = new()
    {
        ["OpenId:Server:Settings:require_pkce"] = "false",
        ["OpenId:Server:Settings:pkce_nonce_exemption"] = "true",
        ["OpenId:Server:Settings:registration_endpoint_allow_anonymous"] = "true",
    };

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
        string clientId,
        string extraQuery
    )
    {
        var query =
            $"?client_id={Uri.EscapeDataString(clientId)}"
            + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
            + "&response_type=code"
            + "&scope=openid"
            + "&state=abc123"
            + "&prompt=none"
            + extraQuery;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/oauth2/authorize" + query);
        request.Headers.Add("Cookie", $"{InteractiveLogin.SessionCookieName}={sessionCookie}");
        return await client.SendAsync(request);
    }

    private static async Task<string> GetCodeAsync(
        HttpClient client,
        string sessionCookie,
        string clientId,
        string extraQuery
    )
    {
        using var response = await AuthorizeAsync(client, sessionCookie, clientId, extraQuery);
        var parameters = GetRedirectParameters(response);

        var code = parameters["code"];
        Assert.True(!string.IsNullOrEmpty(code), $"error='{parameters["error"]}'");
        return code;
    }

    private static System.Collections.Specialized.NameValueCollection GetRedirectParameters(
        HttpResponseMessage response
    )
    {
        var location = response.Headers.Location;
        Assert.NotNull(location);
        return HttpUtility.ParseQueryString(location.Query);
    }

    private static async Task<(HttpStatusCode StatusCode, JsonElement Body)> RedeemAsync(
        HttpClient client,
        string code,
        Dictionary<string, string> clientParameters,
        AuthenticationHeaderValue? authorization = null
    )
    {
        var form = new Dictionary<string, string>(clientParameters)
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
        };
        using var request = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token");
        request.Headers.Authorization = authorization;
        request.Content = new FormUrlEncodedContent(form);

        using var response = await client.SendAsync(request);
        var body = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        return (response.StatusCode, body);
    }

    private static async Task<(
        string ClientId,
        string ClientSecret
    )> RegisterConfidentialClientAsync(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync(
            "/oauth2/register",
            new Dictionary<string, object>
            {
                ["redirect_uris"] = new[] { RedirectUri },
                ["token_endpoint_auth_method"] = OpenIdConstants
                    .ClientAuthenticationMethods
                    .ClientSecretBasic,
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);

        var registration = JsonElement.Parse(body);
        var clientId = registration.GetProperty("client_id").GetString();
        var clientSecret = registration.GetProperty("client_secret").GetString();
        Assert.NotNull(clientId);
        Assert.NotNull(clientSecret);
        return (clientId, clientSecret);
    }

    private static AuthenticationHeaderValue CreateBasicAuthorization(
        string clientId,
        string clientSecret
    )
    {
        var credentials = $"{Uri.EscapeDataString(clientId)}:{Uri.EscapeDataString(clientSecret)}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(credentials));
        return new AuthenticationHeaderValue("Basic", encoded);
    }

    #endregion

    #region Default (PKCE required) Tests

    [Fact]
    public async Task CodeExchange_WithTheMatchingVerifier_IssuesTokens()
    {
        const string clientId = "it-pkce-public";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(clientId, RedirectUri);
        var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        var code = await GetCodeAsync(client, sessionCookie, clientId, TestPkce.QueryParameters);
        var (statusCode, body) = await RedeemAsync(
            client,
            code,
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["code_verifier"] = TestPkce.CodeVerifier,
            }
        );

        Assert.True(statusCode == HttpStatusCode.OK, body.ToString());
        Assert.True(body.TryGetProperty("access_token", out _));
    }

    [Fact]
    public async Task CodeExchange_WithAnotherVerifier_ReturnsInvalidGrant()
    {
        const string clientId = "it-pkce-public-mismatch";
        const string otherVerifier = "OTHERJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
        using var factory = new HostApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        await factory.SeedPublicClientAsync(clientId, RedirectUri);
        var client = CreateClient(factory);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        var code = await GetCodeAsync(client, sessionCookie, clientId, TestPkce.QueryParameters);
        var (statusCode, body) = await RedeemAsync(
            client,
            code,
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["code_verifier"] = otherVerifier,
            }
        );

        Assert.Equal(HttpStatusCode.BadRequest, statusCode);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Authorize_WhenAConfidentialClientUsesOnlyTheNonce_IsRefusedByDefault()
    {
        // require_pkce is in force by default, so the nonce does not stand in for PKCE.
        using var factory = new AnonymousRegistrationApplicationFactory();
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);
        var (clientId, _) = await RegisterConfidentialClientAsync(client);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var response = await AuthorizeAsync(
            client,
            sessionCookie,
            clientId,
            $"&nonce={Nonce}"
        );

        Assert.Equal("invalid_request", GetRedirectParameters(response)["error"]);
    }

    #endregion

    #region Nonce Exemption Tests

    [Fact]
    public async Task CodeExchange_UnderTheNonceExemption_IssuesTokensToTheAuthenticatedClient()
    {
        using var factory = new ConfiguredHostApplicationFactory(NonceExemptionConfiguration);
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);
        var (clientId, clientSecret) = await RegisterConfidentialClientAsync(client);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        var code = await GetCodeAsync(client, sessionCookie, clientId, $"&nonce={Nonce}");
        var (statusCode, body) = await RedeemAsync(
            client,
            code,
            [],
            CreateBasicAuthorization(clientId, clientSecret)
        );

        // The ID Token carries the nonce back, which is how the client detects an injected code.
        Assert.True(statusCode == HttpStatusCode.OK, body.ToString());
        Assert.True(body.TryGetProperty("id_token", out _));
    }

    [Fact]
    public async Task CodeExchange_UnderTheNonceExemption_RefusesARedemptionWithoutClientAuthentication()
    {
        using var factory = new ConfiguredHostApplicationFactory(NonceExemptionConfiguration);
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);
        var (clientId, _) = await RegisterConfidentialClientAsync(client);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        var code = await GetCodeAsync(client, sessionCookie, clientId, $"&nonce={Nonce}");
        var (statusCode, body) = await RedeemAsync(
            client,
            code,
            new Dictionary<string, string> { ["client_id"] = clientId }
        );

        // A code that is not bound to PKCE is worthless to anyone who cannot authenticate as the client.
        Assert.Equal(HttpStatusCode.BadRequest, statusCode);
        Assert.Equal("invalid_grant", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Authorize_UnderTheNonceExemption_RefusesARequestWithoutANonce()
    {
        // A request that uses neither PKCE nor the nonce leaves the code unprotected (OAuth 2.1 §7.5.1.1).
        using var factory = new ConfiguredHostApplicationFactory(NonceExemptionConfiguration);
        await factory.SeedLocalAccountAsync(UserName, Password);
        var client = CreateClient(factory);
        var (clientId, _) = await RegisterConfidentialClientAsync(client);
        var sessionCookie = await InteractiveLogin.SignInAsync(client, UserName, Password);

        using var response = await AuthorizeAsync(client, sessionCookie, clientId, string.Empty);

        Assert.Equal("invalid_request", GetRedirectParameters(response)["error"]);
    }

    #endregion
}
