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
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NCode.Buffers;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

public class TokenEndpointBootstrapAdminTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";

    // The server reads the configured secret Base64Url-encoded (the client still presents the plaintext).
    private static readonly string BootstrapClientSecretConfig = Base64Url.EncodeToString(
        SecureEncoding.UTF8.GetBytes(BootstrapClientSecret)
    );
    private const string ManagementAudience = "urn:ncode:management";
    private const string ManagementScope = "read:clients";

    [Fact]
    public async Task PostToken_WithConfiguredBootstrapAdmin_SelfSeedsAndIssuesGlobalAdminToken()
    {
        using var factory = new HostApplicationFactory();

        // The bootstrap credential is supplied out of band (ADR-0044); nothing is pre-seeded.
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, config) =>
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                            ["BootstrapAdmin:ClientSecret"] = BootstrapClientSecretConfig,
                            ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
                        }
                    )
            )
        );

        var client = configured.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        // The first token request provisions the tenant (seeding the bootstrap client) before client authentication,
        // so this single call self-bootstraps.
        using var request = CreateBootstrapTokenRequest();
        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        Assert.True(root.TryGetProperty("access_token", out var accessToken));
        Assert.False(string.IsNullOrEmpty(accessToken.GetString()));

        using var payload = DecodeJwtPayload(accessToken.GetString()!);
        var payloadRoot = payload.RootElement;

        // The bootstrap client's management token is stamped with the GlobalAdmin role at issuance (ADR-0044).
        Assert.Equal("GlobalAdmin", payloadRoot.GetProperty("role").GetString());

        var audElement = payloadRoot.GetProperty("aud");
        var audiences =
            audElement.ValueKind == JsonValueKind.Array
                ? audElement.EnumerateArray().Select(element => element.GetString()).ToList()
                : [audElement.GetString()];
        Assert.Contains(ManagementAudience, audiences);
    }

    [Fact]
    public async Task PostToken_WhenTheServerDefaultsAnotherMethod_TheBootstrapClientStillUsesBasic()
    {
        // The bootstrap client registers client_secret_basic itself, so a server-wide default for clients that register
        // no method does not change how it authenticates.
        using var factory = new HostApplicationFactory();
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, config) =>
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                            ["BootstrapAdmin:ClientSecret"] = BootstrapClientSecretConfig,
                            ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
                            ["OpenId:Server:Settings:token_endpoint_auth_method"] = OpenIdConstants
                                .ClientAuthenticationMethods
                                .PrivateKeyJwt,
                        }
                    )
            )
        );
        var client = configured.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var request = CreateBootstrapTokenRequest();
        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
    }

    [Fact]
    public async Task PostToken_WhenTheServerDoesNotPermitClientSecretBasic_RefusesToSeedTheBootstrapClient()
    {
        // The bootstrap client registers client_secret_basic, which the server's ceiling excludes, so the seed refuses
        // to store a client the tenant could never serve.
        using var factory = new HostApplicationFactory();
        using var configured = factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration(
                (_, config) =>
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                            ["BootstrapAdmin:ClientSecret"] = BootstrapClientSecretConfig,
                            ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
                            ["OpenId:Server:Settings:token_endpoint_auth_methods_supported:0"] =
                                OpenIdConstants.ClientAuthenticationMethods.ClientSecretPost,
                        }
                    )
            )
        );
        var client = configured.CreateClient(
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
        using var factory = new BootstrapConfiguredFactory();

        // Stage the bootstrap client with a secret that does not match configuration (as a prior run's now-unusable
        // master keys would leave it). The provisioning pass on the first token request must heal it to the
        // configured secret before client authentication; without the self-heal this request fails invalid_client.
        await factory.SeedClientDirectlyAsync(BootstrapClientId, "stale-secret-value");

        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var request = CreateBootstrapTokenRequest();
        using var response = await client.SendAsync(request);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        using var document = JsonDocument.Parse(body);
        Assert.True(document.RootElement.TryGetProperty("access_token", out var accessToken));
        Assert.False(string.IsNullOrEmpty(accessToken.GetString()));

        using var payload = DecodeJwtPayload(accessToken.GetString()!);
        Assert.Equal("GlobalAdmin", payload.RootElement.GetProperty("role").GetString());
    }

    // The bootstrap client registers client_secret_basic, so it presents its secret with HTTP Basic authentication.
    private static HttpRequestMessage CreateBootstrapTokenRequest()
    {
        var credentials = SecureEncoding.UTF8.GetBytes(
            $"{BootstrapClientId}:{BootstrapClientSecret}"
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

    private static JsonDocument DecodeJwtPayload(string jwt)
    {
        var payloadSegment = jwt.Split('.')[1];
        var payloadBytes = Base64Url.DecodeFromChars(payloadSegment);
        return JsonDocument.Parse(payloadBytes);
    }

    /// <summary>
    /// A factory that supplies the bootstrap administrator credential through configuration, so both the direct
    /// seeding helpers and the configured bootstrap feature operate on the same host and in-memory database.
    /// </summary>
    private sealed class BootstrapConfiguredFactory : HostApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureAppConfiguration(
                (_, config) =>
                    config.AddInMemoryCollection(
                        new Dictionary<string, string?>
                        {
                            ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                            ["BootstrapAdmin:ClientSecret"] = BootstrapClientSecretConfig,
                            ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
                        }
                    )
            );
        }
    }
}
