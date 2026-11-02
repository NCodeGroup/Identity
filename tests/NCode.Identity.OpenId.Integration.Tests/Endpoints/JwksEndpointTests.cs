// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public class JwksEndpointTests(HostApplicationFactory factory)
    : IClassFixture<HostApplicationFactory>
{
    private HostApplicationFactory Factory { get; } = factory;

    [Fact]
    public async Task GetJwks_WithProvisionedKeys_PublishesAnRs256SigningKey()
    {
        var client = Factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.GetAsync("/oauth2/jwks");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var jsonElement = JsonElement.Parse(json);

        Assert.True(jsonElement.TryGetProperty("keys", out var keys));
        Assert.Equal(JsonValueKind.Array, keys.ValueKind);
        Assert.True(keys.GetArrayLength() >= 1);

        // The host provisions an RS256 signing key (ADR-1169), so a valid public JWK must be published.
        var key = keys.EnumerateArray()
            .Single(candidate => candidate.GetProperty("alg").GetString() == "RS256");
        Assert.Equal("RSA", key.GetProperty("kty").GetString());
        Assert.Equal("sig", key.GetProperty("use").GetString());
        Assert.Equal("RS256", key.GetProperty("alg").GetString());
        Assert.False(string.IsNullOrEmpty(key.GetProperty("kid").GetString()));
        Assert.False(string.IsNullOrEmpty(key.GetProperty("n").GetString()));
        Assert.False(string.IsNullOrEmpty(key.GetProperty("e").GetString()));
    }
}
