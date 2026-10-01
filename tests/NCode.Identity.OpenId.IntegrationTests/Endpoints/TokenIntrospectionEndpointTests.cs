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
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class TokenIntrospectionEndpointTests
{
    private const string ClientId = "it-introspect-client";
    private const string ClientSecret = "confidential-secret-value";

    [Fact]
    public async Task PostIntrospect_WithUnknownToken_ReturnsInactive()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["token"] = "not-a-real-token",
            }
        );

        using var response = await client.PostAsync("/oauth2/introspect", content);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.GetProperty("active").GetBoolean());
    }

    [Fact]
    public async Task PostIntrospect_WithoutToken_ReturnsInvalidRequest()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
            }
        );

        using var response = await client.PostAsync("/oauth2/introspect", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_request", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostIntrospect_WithoutClientAuthentication_IsRejected()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["token"] = "not-a-real-token" }
        );

        using var response = await client.PostAsync("/oauth2/introspect", content);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostIntrospect_WithActiveAccessToken_ReturnsActiveWithClaims()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-introspect-rs",
            "https://api.introspect.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        // Obtain a real access token via the client-credentials flow.
        using var tokenContent = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["scope"] = TestServerSettingsProvider.ApiScope,
            }
        );

        using var tokenResponse = await client.PostAsync("/oauth2/token", tokenContent);
        var tokenBody = await tokenResponse.Content.ReadAsStringAsync();
        Assert.True(tokenResponse.StatusCode == HttpStatusCode.OK, tokenBody);

        using var tokenDocument = JsonDocument.Parse(tokenBody);
        var accessToken = tokenDocument.RootElement.GetProperty("access_token").GetString();
        Assert.False(string.IsNullOrEmpty(accessToken));

        // Introspect the access token.
        using var introspectContent = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["token"] = accessToken!,
                ["token_type_hint"] = "access_token",
            }
        );

        using var response = await client.PostAsync("/oauth2/introspect", introspectContent);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.True(root.GetProperty("active").GetBoolean());

        // The JWT claims are flattened into the top-level introspection response.
        Assert.True(root.TryGetProperty("iss", out _));
        Assert.True(root.TryGetProperty("exp", out _));
    }
}
