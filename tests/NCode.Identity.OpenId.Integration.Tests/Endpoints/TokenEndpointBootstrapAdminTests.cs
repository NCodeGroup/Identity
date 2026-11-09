// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

public class TokenEndpointBootstrapAdminTests
{
    private const string ManagementAudience = "urn:ncode:management";
    private const string ManagementScope = "read:clients";

    [Fact]
    public async Task PostToken_WithConfiguredBootstrapAdmin_SelfSeedsAndIssuesGlobalAdminToken()
    {
        // The bootstrap credential is supplied out of band (ADR-1044); nothing is pre-seeded.
        using var factory = new BootstrapAdminApplicationFactory();

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        // The first token request provisions the tenant (seeding the bootstrap client) before client authentication,
        // so this single call self-bootstraps.
        using var request = CreateBootstrapTokenRequest();
        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        var root = jsonElement;
        Assert.True(root.TryGetProperty("access_token", out var accessToken));
        Assert.False(string.IsNullOrEmpty(accessToken.GetString()));

        var payload = DecodeJwtPayload(accessToken.GetString()!);
        var payloadRoot = payload;

        // The bootstrap client's management token is stamped with the GlobalAdmin role at issuance (ADR-1044).
        Assert.Equal("GlobalAdmin", payloadRoot.GetProperty("role").GetString());

        var audElement = payloadRoot.GetProperty("aud");
        var audiences =
            audElement.ValueKind == JsonValueKind.Array
                ? audElement.EnumerateArray().Select(element => element.GetString()).ToList()
                : [audElement.GetString()];
        Assert.Contains(ManagementAudience, audiences);
    }

    [Fact]
    public async Task PostToken_WhenThePlatformDefaultsAnotherMethod_TheBootstrapClientStillUsesBasic()
    {
        // The bootstrap client registers client_secret_basic itself, so a platform-wide default for clients that register
        // no method does not change how it authenticates.
        using var factory = new BootstrapAdminApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:Platform:Settings:token_endpoint_auth_method"] = OpenIdProtocol
                    .ClientAuthenticationMethods
                    .PrivateKeyJwt,
            }
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var request = CreateBootstrapTokenRequest();
        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task PostToken_WhenThePlatformDoesNotPermitClientSecretBasic_RefusesToSeedTheBootstrapClient()
    {
        // The bootstrap client registers client_secret_basic, which the platform's ceiling excludes, so the seed refuses
        // to store a client the tenant could never serve.
        using var factory = new BootstrapAdminApplicationFactory(
            new Dictionary<string, string?>
            {
                ["OpenId:Platform:Settings:token_endpoint_auth_methods_supported:0"] =
                    OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost,
            }
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        // The first request provisions the tenant, which runs the seed and fails; the developer exception page answers.
        using var request = CreateBootstrapTokenRequest();
        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("cannot be seeded", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PostToken_WhenBootstrapNotConfigured_DoesNotIssueTokenForBootstrapClient()
    {
        using var factory = new HostApplicationFactory();

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var request = CreateBootstrapTokenRequest();
        using var response = await client.SendAsync(request);

        // Absent configuration, the bootstrap client is never seeded, so there is no client to authenticate.
        Assert.NotEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PostToken_WhenBootstrapClientHasStaleSecret_HealsAndIssuesToken()
    {
        using var factory = new BootstrapAdminApplicationFactory();

        // Stage the bootstrap client with a secret that does not match configuration (as a prior run's now-unusable
        // master keys would leave it). The provisioning pass on the first token request must heal it to the
        // configured secret before client authentication; without the self-heal this request fails invalid_client.
        await factory.SeedClientDirectlyAsync(
            BootstrapAdminApplicationFactory.ClientId,
            "stale-secret-value"
        );

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var request = CreateBootstrapTokenRequest();
        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        Assert.True(jsonElement.TryGetProperty("access_token", out var accessToken));
        Assert.False(string.IsNullOrEmpty(accessToken.GetString()));

        var payload = DecodeJwtPayload(accessToken.GetString()!);
        Assert.Equal("GlobalAdmin", payload.GetProperty("role").GetString());
    }

    // The bootstrap client registers client_secret_basic, so it presents its secret with HTTP Basic authentication.
    private static HttpRequestMessage CreateBootstrapTokenRequest()
    {
        var credentials = SecureEncoding.UTF8.GetBytes(
            $"{BootstrapAdminApplicationFactory.ClientId}:{BootstrapAdminApplicationFactory.ClientSecret}"
        );
        return new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
        {
            Headers =
            {
                Authorization = new AuthenticationHeaderValue(
                    "Basic",
                    Convert.ToBase64String(credentials)
                ),
            },
            Content = new FormUrlEncodedContent(
                new Dictionary<string, string>
                {
                    ["grant_type"] = OpenIdProtocol.GrantTypes.ClientCredentials,
                    ["scope"] = ManagementScope,
                }
            ),
        };
    }

    private static JsonElement DecodeJwtPayload(string jwt)
    {
        var payloadSegment = jwt.Split('.')[1];
        var payloadBytes = Base64Url.DecodeFromChars(payloadSegment);
        return JsonElement.Parse(payloadBytes);
    }
}
