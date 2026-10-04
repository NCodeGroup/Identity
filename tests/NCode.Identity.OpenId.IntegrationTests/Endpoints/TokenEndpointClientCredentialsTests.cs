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
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class TokenEndpointClientCredentialsTests
{
    private const string ClientId = "it-cc-client";
    private const string ClientSecret = "confidential-secret-value";

    [Fact]
    public async Task PostToken_WithClientCredentials_ReturnsAccessToken()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-rs-api",
            "https://api.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["scope"] = TestServerSettingsProvider.ApiScope,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        Assert.True(root.TryGetProperty("access_token", out var accessToken));
        Assert.False(string.IsNullOrEmpty(accessToken.GetString()));

        Assert.Equal(OpenIdConstants.TokenTypes.Bearer, root.GetProperty("token_type").GetString());
        Assert.Equal(TestServerSettingsProvider.ApiScope, root.GetProperty("scope").GetString());

        // The access token audience is bound to the resource server that owns the scope, not the client (ADR-0027).
        using var payload = DecodeJwtPayload(accessToken.GetString()!);
        var audElement = payload.RootElement.GetProperty("aud");
        var audiences =
            audElement.ValueKind == JsonValueKind.Array
                ? audElement.EnumerateArray().Select(element => element.GetString()).ToList()
                : [audElement.GetString()];
        Assert.Contains("https://api.integration.test", audiences);
        Assert.DoesNotContain(ClientId, audiences);
    }

    [Fact]
    public async Task PostToken_WithWrongClientSecret_DoesNotReturnAccessToken()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = "wrong-secret",
                ["scope"] = TestServerSettingsProvider.ApiScope,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task PostToken_ClientAuthenticationResult_IsScopedPerRequest()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-rs-api",
            "https://api.integration.test",
            TestServerSettingsProvider.ApiScope
        );

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        // A wrong secret must be rejected, a later correct secret must still succeed, and a wrong secret after a
        // success must still be rejected. If the client-authentication result were cached on the singleton service
        // instead of the request, the first result would stick for every subsequent request.
        using var wrong = await client.PostAsync("/oauth2/token", CreateBody("wrong-secret"));
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        using var correct = await client.PostAsync("/oauth2/token", CreateBody(ClientSecret));
        var correctBody = await correct.Content.ReadAsStringAsync();
        Assert.True(correct.StatusCode == HttpStatusCode.OK, correctBody);

        using var wrongAgain = await client.PostAsync("/oauth2/token", CreateBody("wrong-secret"));
        Assert.Equal(HttpStatusCode.BadRequest, wrongAgain.StatusCode);
    }

    private static FormUrlEncodedContent CreateBody(string clientSecret) =>
        new(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdConstants.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = clientSecret,
                ["scope"] = TestServerSettingsProvider.ApiScope,
            }
        );

    private static JsonDocument DecodeJwtPayload(string jwt)
    {
        var payloadSegment = jwt.Split('.')[1];
        var payloadBytes = Base64Url.DecodeFromChars(payloadSegment);
        return JsonDocument.Parse(payloadBytes);
    }
}
