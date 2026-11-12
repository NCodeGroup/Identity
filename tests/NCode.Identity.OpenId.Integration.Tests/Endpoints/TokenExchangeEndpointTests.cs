// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public class TokenExchangeEndpointTests
{
    private const string ClientId = "it-tx-client";
    private const string ClientSecret = "confidential-secret-value";

    // The platform ceiling lists the grants the test client uses; token exchange is not in the library baseline.
    private static Dictionary<string, string?> Configuration { get; } =
        new()
        {
            ["OpenId:Platform:Settings:grant_types_supported:0"] = OpenIdProtocol
                .GrantTypes
                .ClientCredentials,
            ["OpenId:Platform:Settings:grant_types_supported:1"] = OpenIdProtocol
                .GrantTypes
                .TokenExchange,
        };

    // See ADR-1466.
    [Fact]
    public async Task PostToken_WhenTheSubjectTokenIsAClientCredentialsAccessToken_ReturnsInvalidGrant()
    {
        using var factory = new ConfiguredHostApplicationFactory(Configuration);
        await factory.SeedConfidentialClientAsync(
            ClientId,
            ClientSecret,
            [OpenIdProtocol.GrantTypes.ClientCredentials, OpenIdProtocol.GrantTypes.TokenExchange],
            CancellationToken.None
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-rs-api",
            "https://api.integration.test",
            [TestPlatformSettingsProvider.ApiScope],
            CancellationToken.None
        );

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var credentialsContent = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["scope"] = TestPlatformSettingsProvider.ApiScope,
            }
        );
        using var credentialsResponse = await client.PostAsync("/oauth2/token", credentialsContent);
        var credentialsBody = await credentialsResponse.Content.ReadAsStringAsync();
        Assert.True(credentialsResponse.StatusCode == HttpStatusCode.OK, credentialsBody);
        var accessToken = JsonElement
            .Parse(credentialsBody)
            .GetProperty("access_token")
            .GetString();
        Assert.False(string.IsNullOrEmpty(accessToken));

        // The token names the client's own principal, which no exchange accepts.
        using var exchangeContent = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.TokenExchange,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["subject_token"] = accessToken,
                ["subject_token_type"] = OpenIdProtocol.TokenTypeIdentifiers.AccessToken,
            }
        );
        using var exchangeResponse = await client.PostAsync("/oauth2/token", exchangeContent);

        var exchangeBody = await exchangeResponse.Content.ReadAsStringAsync();
        Assert.True(exchangeResponse.StatusCode == HttpStatusCode.BadRequest, exchangeBody);

        var root = JsonElement.Parse(exchangeBody);
        Assert.Equal("invalid_grant", root.GetProperty("error").GetString());
        Assert.Contains(
            "represents a client acting for itself",
            root.GetProperty("error_description").GetString()
        );
    }
}
