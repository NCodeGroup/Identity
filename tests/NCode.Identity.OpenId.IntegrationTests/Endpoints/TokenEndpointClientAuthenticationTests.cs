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
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

/// <summary>
/// Verifies client authentication at the token endpoint: a client authenticates only with the method it is registered to
/// use (RFC 7591 §2), within the server's <c>token_endpoint_auth_methods_supported</c>; and, per RFC 6749 §5.2, a client
/// that authenticates via the HTTP <c>Authorization</c> request header (<c>client_secret_basic</c>) and fails gets
/// <c>401 Unauthorized</c> with a matching <c>WWW-Authenticate</c> challenge, while failures via the request body
/// (<c>client_secret_post</c>) stay <c>400</c>.
/// </summary>
public class TokenEndpointClientAuthenticationTests
{
    private const string ClientId = "it-auth-client";
    private const string ClientSecret = "confidential-secret-value";
    private const string TokenPath = "/oauth2/token";

    private static HttpClient CreateClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static AuthenticationHeaderValue BasicHeader(string clientId, string clientSecret)
    {
        var raw = $"{clientId}:{clientSecret}";
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(raw));
        return new AuthenticationHeaderValue("Basic", encoded);
    }

    private static FormUrlEncodedContent ClientCredentialsBody() =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["scope"] = TestServerSettingsProvider.ApiScope,
            }
        );

    private static FormUrlEncodedContent ClientSecretPostBody(string clientSecret) =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = clientSecret,
                ["scope"] = TestServerSettingsProvider.ApiScope,
            }
        );

    private static async Task SeedApiGrantAsync(HostApplicationFactory factory) =>
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-auth-rs-api",
            "https://api.auth.integration.test",
            TestServerSettingsProvider.ApiScope
        );

    private static async Task<HttpResponseMessage> SendBasicAsync(
        HttpClient client,
        string clientSecret
    )
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Headers = { Authorization = BasicHeader(ClientId, clientSecret) },
            Content = ClientCredentialsBody(),
        };

        return await client.SendAsync(request);
    }

    private static async Task AssertInvalidClientAsync(
        HttpResponseMessage response,
        HttpStatusCode expectedStatusCode
    )
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == expectedStatusCode, body);

        using var document = JsonDocument.Parse(body);
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidClient,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    #region Registered Method Tests

    [Fact]
    public async Task PostToken_WhenAConfidentialClientPresentsOnlyItsClientId_ReturnsInvalidClient()
    {
        // A client registered as confidential never authenticates as public, whatever grant it requests.
        using var factory = new HostApplicationFactory();
        await factory.SeedClientSecretBasicClientAsync(ClientId, ClientSecret);
        var client = CreateClient(factory);

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.RefreshToken,
                ["client_id"] = ClientId,
                ["refresh_token"] = "any-refresh-token",
            }
        );
        using var response = await client.PostAsync(TokenPath, content);

        await AssertInvalidClientAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostToken_WhenAClientSecretBasicClientPostsItsSecret_ReturnsInvalidClient()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedClientSecretBasicClientAsync(ClientId, ClientSecret);
        await SeedApiGrantAsync(factory);
        var client = CreateClient(factory);

        using var content = ClientSecretPostBody(ClientSecret);
        using var response = await client.PostAsync(TokenPath, content);

        await AssertInvalidClientAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostToken_WhenAClientSecretPostClientUsesBasicAuth_Returns401WithChallenge()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);
        await SeedApiGrantAsync(factory);
        var client = CreateClient(factory);

        using var response = await SendBasicAsync(client, ClientSecret);

        await AssertInvalidClientAsync(response, HttpStatusCode.Unauthorized);
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Basic");
    }

    [Fact]
    public async Task PostToken_WhenAPublicClientPresentsASecret_ReturnsInvalidClient()
    {
        // A public client has no secret (RFC 7591 §2 'none'), so one it presents authenticates nothing.
        using var factory = new HostApplicationFactory();
        await factory.SeedPublicClientAsync(ClientId, "https://client.example/callback");
        var client = CreateClient(factory);

        using var content = ClientSecretPostBody(ClientSecret);
        using var response = await client.PostAsync(TokenPath, content);

        await AssertInvalidClientAsync(response, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostToken_WhenTheClientRegistersNoMethod_AuthenticatesOnlyWithClientSecretBasic()
    {
        // RFC 7591 §2: a client that registers no token_endpoint_auth_method uses client_secret_basic.
        using var factory = new HostApplicationFactory();
        await factory.SeedClientWithoutAuthenticationMethodAsync(ClientId, ClientSecret);
        await SeedApiGrantAsync(factory);
        var client = CreateClient(factory);

        using var basicResponse = await SendBasicAsync(client, ClientSecret);
        using var postContent = ClientSecretPostBody(ClientSecret);
        using var postResponse = await client.PostAsync(TokenPath, postContent);

        var basicBody = await basicResponse.Content.ReadAsStringAsync();
        Assert.True(basicResponse.StatusCode == HttpStatusCode.OK, basicBody);
        await AssertInvalidClientAsync(postResponse, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostToken_WhenTheServerDoesNotPermitTheRegisteredMethod_ReturnsInvalidClient()
    {
        // token_endpoint_auth_methods_supported is a ceiling: a client registered outside it cannot authenticate.
        using var factory = new ConfiguredHostApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:Server:Settings:token_endpoint_auth_methods_supported:0"] = OpenIdConstants
                    .ClientAuthenticationMethods
                    .ClientSecretPost,
            }
        );
        await factory.SeedClientSecretBasicClientAsync(ClientId, ClientSecret);
        await SeedApiGrantAsync(factory);
        var client = CreateClient(factory);

        using var response = await SendBasicAsync(client, ClientSecret);

        await AssertInvalidClientAsync(response, HttpStatusCode.Unauthorized);
    }

    #endregion

    #region Challenge Tests
    [Fact]
    public async Task PostToken_BasicAuthWithWrongSecret_Returns401WithChallenge()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedClientSecretBasicClientAsync(ClientId, ClientSecret);

        var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Headers = { Authorization = BasicHeader(ClientId, "wrong-secret") },
            Content = ClientCredentialsBody(),
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal("Basic", challenge.Scheme);
        Assert.Contains("realm=", challenge.Parameter);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidClient,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    [Fact]
    public async Task PostToken_BasicAuthWithUnknownClient_Returns401WithChallenge()
    {
        using var factory = new HostApplicationFactory();
        // Warm up the tenant so the request reaches client authentication.
        using (var warmup = CreateClient(factory))
        {
            using var _ = await warmup.GetAsync("/oauth2/jwks");
        }

        var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Headers = { Authorization = BasicHeader("no-such-client", "whatever") },
            Content = ClientCredentialsBody(),
        };

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, challenge => challenge.Scheme == "Basic");
    }

    [Fact]
    public async Task PostToken_BasicAuthWithCorrectSecret_ReturnsAccessToken()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedClientSecretBasicClientAsync(ClientId, ClientSecret);
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-auth-rs-api",
            "https://api.auth.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = CreateClient(factory);

        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Headers = { Authorization = BasicHeader(ClientId, ClientSecret) },
            Content = ClientCredentialsBody(),
        };

        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.False(
            string.IsNullOrEmpty(document.RootElement.GetProperty("access_token").GetString())
        );
    }

    [Fact]
    public async Task PostToken_PostAuthWithWrongSecret_Returns400WithoutChallenge()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);

        var client = CreateClient(factory);

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = "wrong-secret",
                ["scope"] = TestServerSettingsProvider.ApiScope,
            }
        );
        using var response = await client.PostAsync(TokenPath, content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(response.Headers.WwwAuthenticate);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            OpenIdConstants.ErrorCodes.InvalidClient,
            document.RootElement.GetProperty("error").GetString()
        );
    }

    #endregion
}
