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
/// Verifies the token endpoint honors RFC 6749 §5.2: when a client authenticates via the HTTP <c>Authorization</c>
/// request header (<c>client_secret_basic</c>) and fails, the response is <c>401 Unauthorized</c> with a matching
/// <c>WWW-Authenticate</c> challenge; failures via the request body (<c>client_secret_post</c>) stay <c>400</c>.
/// </summary>
public class TokenEndpointClientAuthenticationTests
{
    private const string ClientId = "it-auth-client";
    private const string ClientSecret = "confidential-secret-value";
    private const string TokenPath = "/oauth2/token";

    private static HttpClient CreateClient(PlaygroundApplicationFactory factory) =>
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

    [Fact]
    public async Task PostToken_BasicAuthWithWrongSecret_Returns401WithChallenge()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);

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
        using var factory = new PlaygroundApplicationFactory();
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
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);
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
        using var factory = new PlaygroundApplicationFactory();
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
}
