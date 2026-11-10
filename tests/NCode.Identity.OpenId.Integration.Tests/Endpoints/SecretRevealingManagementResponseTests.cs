// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// Every management response that reveals a secret is marked so that no cache stores it (ADR-1345).
public class SecretRevealingManagementResponseTests
{
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";

    private const string ResourceServerClientId = "it-no-store-client";
    private const string ResourceServerClientSecret = "confidential-secret-value";
    private const string ResourceServerId = "it-no-store-rs";
    private const string ResourceServerAudience = "https://no-store.integration.test";
    private const string ResourceServerScope = "no-store:read";

    #region PostClientSecret Tests

    [Fact]
    public async Task PostClientSecret_Always_MarksTheResponseNoStore()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var clientId = await CreateClientAsync(administrator);

        using var response = await PostClientSecretAsync(administrator, clientId);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        NoStoreAssert.IsNoStore(response);
    }

    #endregion

    #region PostRotateClientSecret Tests

    [Fact]
    public async Task PostRotateClientSecret_Always_MarksTheResponseNoStore()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var clientId = await CreateClientAsync(administrator);
        var secretId = await CreateSecretAsync(administrator, clientId);
        var expiresWhen = DateTimeOffset.UtcNow.AddYears(1);

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets/{secretId}/rotate",
            new { expiresWhen }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        NoStoreAssert.IsNoStore(response);
    }

    #endregion

    #region PostResourceServerSecret Tests

    [Fact]
    public async Task PostResourceServerSecret_Always_MarksTheResponseNoStore()
    {
        using var factory = CreateFactory();
        await factory.SeedConfidentialClientAsync(
            ResourceServerClientId,
            ResourceServerClientSecret,
            [],
            CancellationToken.None
        );
        await factory.SeedResourceServerWithClientGrantAsync(
            ResourceServerClientId,
            ResourceServerId,
            ResourceServerAudience,
            [ResourceServerScope],
            CancellationToken.None
        );
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var expiresWhen = DateTimeOffset.UtcNow.AddYears(1);

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/resource-servers/{ResourceServerId}/secrets",
            new
            {
                secretType = "rsa",
                keySizeBits = 2048,
                use = "enc",
                expiresWhen,
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        NoStoreAssert.IsNoStore(response);
    }

    #endregion

    #region PostInitialAccessToken Tests

    [Fact]
    public async Task PostInitialAccessToken_Always_MarksTheResponseNoStore()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/grants/initial-access-tokens",
            new { description = "no-store" }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        NoStoreAssert.IsNoStore(response);
    }

    #endregion

    #region Scaffolding

    private static BootstrapAdminApplicationFactory CreateFactory() => new();

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

    private static Task<HttpResponseMessage> PostClientSecretAsync(
        HttpClient administrator,
        string clientId
    )
    {
        var expiresWhen = DateTimeOffset.UtcNow.AddYears(1);
        return administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen,
            }
        );
    }

    private static async Task<string> CreateSecretAsync(HttpClient administrator, string clientId)
    {
        using var response = await PostClientSecretAsync(administrator, clientId);
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

    #endregion
}
