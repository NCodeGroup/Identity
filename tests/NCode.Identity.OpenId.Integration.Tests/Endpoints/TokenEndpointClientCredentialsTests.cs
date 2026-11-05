// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public class TokenEndpointClientCredentialsTests
{
    private const string ClientId = "it-cc-client";
    private const string ClientSecret = "confidential-secret-value";

    [Fact]
    public async Task PostToken_WithClientCredentials_ReturnsAccessToken()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-rs-api",
            "https://api.integration.test",
            TestPlatformSettingsProvider.ApiScope
        );

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["scope"] = TestPlatformSettingsProvider.ApiScope,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        var root = jsonElement;

        Assert.True(root.TryGetProperty("access_token", out var accessToken));
        Assert.False(string.IsNullOrEmpty(accessToken.GetString()));

        Assert.Equal(OpenIdProtocol.TokenTypes.Bearer, root.GetProperty("token_type").GetString());
        Assert.Equal(TestPlatformSettingsProvider.ApiScope, root.GetProperty("scope").GetString());

        // The access token audience is bound to the resource server that owns the scope, not the client (ADR-1027).
        var payload = DecodeJwtPayload(accessToken.GetString()!);
        var audElement = payload.GetProperty("aud");
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
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = "wrong-secret",
                ["scope"] = TestPlatformSettingsProvider.ApiScope,
            }
        );

        using var response = await client.PostAsync("/oauth2/token", content);

        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);

        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(jsonElement.TryGetProperty("error", out _));
    }

    [Fact]
    public async Task PostToken_ClientAuthenticationResult_IsScopedPerRequest()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedConfidentialClientAsync(ClientId, ClientSecret);
        await factory.SeedResourceServerWithClientGrantAsync(
            ClientId,
            "it-rs-api",
            "https://api.integration.test",
            TestPlatformSettingsProvider.ApiScope
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
                ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                ["client_id"] = ClientId,
                ["client_secret"] = clientSecret,
                ["scope"] = TestPlatformSettingsProvider.ApiScope,
            }
        );

    private static JsonElement DecodeJwtPayload(string jwt)
    {
        var payloadSegment = jwt.Split('.')[1];
        var payloadBytes = Base64Url.DecodeFromChars(payloadSegment);
        return JsonElement.Parse(payloadBytes);
    }
}
