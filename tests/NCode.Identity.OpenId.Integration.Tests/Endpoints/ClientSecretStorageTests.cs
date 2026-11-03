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
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.Persistence;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A client secret the server only compares is stored as a one-way verifier; a client registered for client_secret_jwt,
// which keys an HMAC from its secret, is issued one the server can read back. The stored form always matches the method
// the client is registered for (ADR-1391).
public class ClientSecretStorageTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string ManagementScope = "read:clients";
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";

    [Fact]
    public async Task PostSecret_ForAClientThatComparesItsSecret_StoresOnlyAVerifierAndRevealsTheSecretOnce()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateHttpClient(factory);

        var clientId = await CreateClientAsync(
            administrator,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );
        var (secretId, secretMaterial) = await CreateSecretAsync(administrator, clientId);

        var stored = Assert.Single(await factory.GetClientSecretsAsync(clientId));
        Assert.Equal(secretId, stored.SecretId);
        Assert.Equal(SecretEncodings.Verifier, stored.SecretEncoding);
        Assert.Null(
            await ClientAuthenticationProbe.RevokeWithSecretAsync(client, clientId, secretMaterial)
        );
        Assert.Equal(
            "invalid_client",
            await ClientAuthenticationProbe.RevokeWithSecretAsync(
                client,
                clientId,
                secretMaterial + "x"
            )
        );

        // No read returns the material again.
        using var getResponse = await administrator.GetAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets/{secretId}"
        );
        var getBody = await getResponse.Content.ReadAsStringAsync();
        Assert.True(getResponse.StatusCode == HttpStatusCode.OK, getBody);
        Assert.DoesNotContain(secretMaterial, getBody, StringComparison.Ordinal);
        Assert.False(JsonElement.Parse(getBody).TryGetProperty("secretMaterial", out _));
    }

    [Fact]
    public async Task PostSecret_ForAClientSecretJwtClient_StoresASecretThatKeysTheClientAssertion()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateHttpClient(factory);
        var issuer = await ClientAuthenticationProbe.GetIssuerAsync(client);

        var clientId = await CreateClientAsync(
            administrator,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt
        );
        var (_, secretMaterial) = await CreateSecretAsync(administrator, clientId);

        var stored = Assert.Single(await factory.GetClientSecretsAsync(clientId));
        Assert.Equal(SecretEncodings.SymmetricRaw, stored.SecretEncoding);
        Assert.Null(
            await ClientAuthenticationProbe.RevokeWithAssertionAsync(
                client,
                clientId,
                secretMaterial,
                issuer
            )
        );
    }

    [Fact]
    public async Task PatchSettings_WhenSwitchedToClientSecretJwt_IssuesASecretThatKeysTheAssertionAndRevealsItOnce()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateHttpClient(factory);
        var issuer = await ClientAuthenticationProbe.GetIssuerAsync(client);

        var clientId = await CreateClientAsync(
            administrator,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );
        var (verifiedSecretId, verifiedSecret) = await CreateSecretAsync(administrator, clientId);

        // A verifier is no HMAC key, so the change issues a secret the server can read back, revealed only in its
        // response, and removes the verifier, which cannot authenticate the client with that method.
        var issued = await SetAuthenticationMethodAsync(
            administrator,
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt
        );

        Assert.NotNull(issued);
        var (issuedSecretId, issuedSecret) = issued.Value;
        Assert.NotEqual(verifiedSecretId, issuedSecretId);
        var stored = Assert.Single(await factory.GetClientSecretsAsync(clientId));
        Assert.Equal(issuedSecretId, stored.SecretId);
        Assert.Equal(SecretEncodings.SymmetricRaw, stored.SecretEncoding);
        Assert.False(await IsVerifierAsync(administrator, clientId, issuedSecretId));
        Assert.Null(
            await ClientAuthenticationProbe.RevokeWithAssertionAsync(
                client,
                clientId,
                issuedSecret,
                issuer
            )
        );
        Assert.Equal(
            "invalid_client",
            await ClientAuthenticationProbe.RevokeWithAssertionAsync(
                client,
                clientId,
                verifiedSecret,
                issuer
            )
        );
    }

    [Fact]
    public async Task PatchSettings_WhenSwitchedAwayFromClientSecretJwt_StoresTheSecretAsAVerifier()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateHttpClient(factory);

        var clientId = await CreateClientAsync(
            administrator,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt
        );
        var (secretId, secretMaterial) = await CreateSecretAsync(administrator, clientId);

        var issued = await SetAuthenticationMethodAsync(
            administrator,
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );

        // The client keeps its secret, which the server cannot read back: its octets are replaced by the
        // verifier that fits a generated secret, and a presented copy of it authenticates the client.
        Assert.Null(issued);
        var stored = Assert.Single(await factory.GetClientSecretsAsync(clientId));
        Assert.NotEqual(secretId, stored.SecretId);
        Assert.Equal(SecretEncodings.Verifier, stored.SecretEncoding);
        Assert.Equal(SecretOrigins.Generated, stored.Origin);
        Assert.True(await IsVerifierAsync(administrator, clientId, stored.SecretId));
        Assert.Null(
            await ClientAuthenticationProbe.RevokeWithSecretAsync(client, clientId, secretMaterial)
        );
        Assert.Equal(
            "invalid_client",
            await ClientAuthenticationProbe.RevokeWithSecretAsync(
                client,
                clientId,
                secretMaterial + "x"
            )
        );
    }

    [Fact]
    public async Task PatchSettings_WhenTheMethodIsUnchanged_IssuesNoSecret()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);

        // A client created without a secret keeps none when other settings change, so an edit never issues one.
        var clientId = await CreateClientAsync(
            administrator,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );

        var (statusCode, body) = await PatchSettingsAsync(
            administrator,
            $"{TenantApiPath}/clients/{clientId}/settings",
            "client_name",
            "renamed client"
        );

        Assert.True(statusCode == HttpStatusCode.NoContent, body);
        Assert.Empty(await factory.GetClientSecretsAsync(clientId));
    }

    [Fact]
    public async Task PatchTenantSettings_WhenTheTenantsMethodChanges_KeepsEveryStoredClientsMethod()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateHttpClient(factory);

        var (tenantStatusCode, tenantBody) = await PatchSettingsAsync(
            administrator,
            $"{TenantApiPath}/settings",
            "token_endpoint_auth_method",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );
        Assert.True(tenantStatusCode == HttpStatusCode.NoContent, tenantBody);

        // The client names no method, so it is registered for the tenant's, and its secret is stored as a verifier.
        using var createResponse = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients",
            new { isDisabled = false, settings = new Dictionary<string, object>() }
        );
        var createBody = await createResponse.Content.ReadAsStringAsync();
        Assert.True(createResponse.StatusCode == HttpStatusCode.Created, createBody);
        var clientId = GetRequiredString(JsonElement.Parse(createBody), "clientId");
        var (secretId, secretMaterial) = await CreateSecretAsync(administrator, clientId);
        Assert.True(await IsVerifierAsync(administrator, clientId, secretId));

        (tenantStatusCode, tenantBody) = await PatchSettingsAsync(
            administrator,
            $"{TenantApiPath}/settings",
            "token_endpoint_auth_method",
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt
        );
        Assert.True(tenantStatusCode == HttpStatusCode.NoContent, tenantBody);

        // The tenant's new method moves no stored client's method, so the client still authenticates with its verifier.
        Assert.Null(
            await ClientAuthenticationProbe.RevokeWithSecretAsync(client, clientId, secretMaterial)
        );
    }

    [Fact]
    public async Task PostClient_WhenAComparedSecretWouldSignRequestObjects_RefusesTheSettings()
    {
        // A secret stored as a one-way verifier keys no HS256 request object. See ADR-1391.
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients",
            new
            {
                isDisabled = false,
                settings = new Dictionary<string, object>
                {
                    ["token_endpoint_auth_method"] = OpenIdProtocol
                        .ClientAuthenticationMethods
                        .ClientSecretBasic,
                    ["request_object_signing_alg_values_supported"] = new[] { "HS256" },
                },
            }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.Contains(
            "request_object_signing_alg_values_supported",
            body,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public async Task BootstrapAdministrator_IsStoredAsAVerifierOfItsConfiguredSecret()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);

        var stored = Assert.Single(await factory.GetClientSecretsAsync(BootstrapClientId));
        Assert.Equal(SecretEncodings.Verifier, stored.SecretEncoding);
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

    private static async Task<string> CreateClientAsync(
        HttpClient administrator,
        string tokenEndpointAuthMethod
    )
    {
        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients",
            new
            {
                isDisabled = false,
                settings = new Dictionary<string, object>
                {
                    ["token_endpoint_auth_method"] = tokenEndpointAuthMethod,
                },
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        return GetRequiredString(JsonElement.Parse(body), "clientId");
    }

    // Returns the secret the change issued, which its response reveals once, or null when the client keeps its secret.
    private static async Task<(
        string SecretId,
        string SecretMaterial
    )?> SetAuthenticationMethodAsync(
        HttpClient administrator,
        string clientId,
        string tokenEndpointAuthMethod
    )
    {
        var operations = new[]
        {
            new
            {
                op = "replace",
                path = "/token_endpoint_auth_method",
                value = tokenEndpointAuthMethod,
            },
        };
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            $"{TenantApiPath}/clients/{clientId}/settings"
        )
        {
            Content = new StringContent(
                JsonSerializer.Serialize(operations),
                SecureEncoding.UTF8,
                "application/json-patch+json"
            ),
        };

        using var response = await administrator.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.NoContent)
        {
            return null;
        }

        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.True(response.Headers.CacheControl?.NoStore, body);
        var jsonElement = JsonElement.Parse(body);
        return (
            GetRequiredString(jsonElement, "secretId"),
            GetRequiredString(jsonElement, "secretMaterial")
        );
    }

    private static async Task<(HttpStatusCode StatusCode, string Body)> PatchSettingsAsync(
        HttpClient administrator,
        string path,
        string settingName,
        string value
    )
    {
        var operations = new[]
        {
            new
            {
                op = "add",
                path = $"/{settingName}",
                value,
            },
        };
        using var request = new HttpRequestMessage(HttpMethod.Patch, path)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(operations),
                SecureEncoding.UTF8,
                "application/json-patch+json"
            ),
        };

        using var response = await administrator.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private static async Task<(string SecretId, string SecretMaterial)> CreateSecretAsync(
        HttpClient administrator,
        string clientId
    )
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
        return (
            GetRequiredString(jsonElement, "secretId"),
            GetRequiredString(jsonElement, "secretMaterial")
        );
    }

    private static async Task<bool> IsVerifierAsync(
        HttpClient administrator,
        string clientId,
        string secretId
    )
    {
        using var response = await administrator.GetAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets/{secretId}"
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonElement.Parse(body).GetProperty("isVerifier").GetBoolean();
    }

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
