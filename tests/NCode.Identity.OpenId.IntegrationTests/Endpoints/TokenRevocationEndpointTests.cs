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

public class TokenRevocationEndpointTests
{
    private const string ClientId = "it-revoke-client";
    private const string ClientSecret = "confidential-secret-value";

    [Fact]
    public async Task PostRevoke_WithUnknownToken_ReturnsOk()
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
                ["token_type_hint"] = "refresh_token",
            }
        );

        using var response = await client.PostAsync("/oauth2/revoke", content);

        // RFC 7009: an unknown token is a no-op and still returns 200.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostRevoke_WithoutToken_ReturnsInvalidRequest()
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

        using var response = await client.PostAsync("/oauth2/revoke", content);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_request", document.RootElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostRevoke_WithoutClientAuthentication_IsRejected()
    {
        using var factory = new PlaygroundApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string> { ["token"] = "not-a-real-token" }
        );

        using var response = await client.PostAsync("/oauth2/revoke", content);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }
}
