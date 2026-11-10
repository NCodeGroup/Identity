// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public class TokenRevocationEndpointTests
{
    private const string ClientId = "it-revoke-client";
    private const string ClientSecret = "confidential-secret-value";

    [Fact]
    public async Task PostRevoke_WithUnknownToken_ReturnsOk()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );

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
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );

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

        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("invalid_request", jsonElement.GetProperty("error").GetString());
    }

    [Fact]
    public async Task PostRevoke_WithoutClientAuthentication_IsRejected()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            [],
            CancellationToken.None
        );

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
