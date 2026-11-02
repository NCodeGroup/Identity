// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// Every management response that reveals a secret is marked so that no cache stores it (ADR-1345).
public class SecretRevealingManagementResponseTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string ManagementScope = "read:clients";
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";

    private const string ResourceServerClientId = "it-no-store-client";
    private const string ResourceServerClientSecret = "confidential-secret-value";
    private const string ResourceServerId = "it-no-store-rs";
    private const string ResourceServerAudience = "https://no-store.integration.test";
    private const string ResourceServerScope = "no-store:read";

    [Fact]
    public async Task PostClientSecret_Always_MarksTheResponseNoStore()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var clientId = await CreateClientAsync(administrator);

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        NoStoreAssert.IsNoStore(response);
    }

    [Fact]
    public async Task PostRotateClientSecret_Always_MarksTheResponseNoStore()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var clientId = await CreateClientAsync(administrator);
        var secretId = await CreateSecretAsync(administrator, clientId);

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets/{secretId}/rotate",
            new { expiresWhen = DateTimeOffset.UtcNow.AddYears(1) }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        NoStoreAssert.IsNoStore(response);
    }

    [Fact]
    public async Task PostResourceServerSecret_Always_MarksTheResponseNoStore()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        await factory.SeedConfidentialClientAsync(
            ResourceServerClientId,
            ResourceServerClientSecret
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ResourceServerClientId,
            ResourceServerId,
            ResourceServerAudience,
            ResourceServerScope
        );
        using var administrator = await CreateAdministratorClientAsync(factory);

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/resource-servers/{ResourceServerId}/secrets",
            new
            {
                secretType = "rsa",
                keySizeBits = 2048,
                use = "enc",
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        NoStoreAssert.IsNoStore(response);
    }

    [Fact]
    public async Task PostInitialAccessToken_Always_MarksTheResponseNoStore()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/grants/initial-access-tokens",
            new { description = "no-store" }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        NoStoreAssert.IsNoStore(response);
    }

    #region Scaffolding

    private static HttpClient CreateHttpClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<HttpClient> CreateAdministratorClientAsync(
        HostApplicationFactory factory
    )
    {
        var client = CreateHttpClient(factory);
        var credentials = SecureEncoding.UTF8.GetBytes(
            $"{BootstrapClientId}:{BootstrapClientSecret}"
        );
        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/oauth2/token")
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

        using var response = await client.SendAsync(tokenRequest);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var jsonElement = JsonElement.Parse(body);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            jsonElement.GetProperty("access_token").GetString()
        );
        return client;
    }

    private static async Task<string> CreateClientAsync(HttpClient administrator)
    {
        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients",
            new
            {
                isDisabled = false,
                settings = new Dictionary<string, object>
                {
                    ["token_endpoint_auth_method"] = OpenIdProtocol
                        .ClientAuthenticationMethods
                        .ClientSecretPost,
                },
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var jsonElement = JsonElement.Parse(body);
        return GetRequiredString(jsonElement, "clientId");
    }

    private static async Task<string> CreateSecretAsync(HttpClient administrator, string clientId)
    {
        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var jsonElement = JsonElement.Parse(body);
        return GetRequiredString(jsonElement, "secretId");
    }

    // Reads a string property the endpoint always writes, failing the test here when the property is null.
    private static string GetRequiredString(JsonElement jsonElement, string propertyName)
    {
        var value = jsonElement.GetProperty(propertyName).GetString();
        Assert.NotNull(value);
        return value;
    }

    private sealed class BootstrapAdminHostApplicationFactory : HostApplicationFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration(configurationBuilder =>
                configurationBuilder.AddInMemoryCollection(
                    new Dictionary<string, string?>
                    {
                        ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                        ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                            SecureEncoding.UTF8.GetBytes(BootstrapClientSecret)
                        ),
                        ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
                    }
                )
            );
        }
    }

    #endregion
}
