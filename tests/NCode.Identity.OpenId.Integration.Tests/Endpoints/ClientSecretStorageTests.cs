// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Secrets;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A client secret the server only compares is stored as a one-way verifier; a client registered for client_secret_jwt,
// which keys an HMAC from its secret, is issued one the server can read back. The form is chosen when the secret is
// issued, from the method the client is registered for then (ADR-1391).
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
        Assert.Null(await AuthenticateWithSecretAsync(client, clientId, secretMaterial));
        Assert.Equal(
            "invalid_client",
            await AuthenticateWithSecretAsync(client, clientId, secretMaterial + "x")
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
        var issuer = await GetIssuerAsync(client);

        var clientId = await CreateClientAsync(
            administrator,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt
        );
        var (_, secretMaterial) = await CreateSecretAsync(administrator, clientId);

        var stored = Assert.Single(await factory.GetClientSecretsAsync(clientId));
        Assert.Equal(SecretEncodings.SymmetricRaw, stored.SecretEncoding);
        Assert.Null(await AuthenticateWithAssertionAsync(client, clientId, secretMaterial, issuer));
    }

    [Fact]
    public async Task PatchSettings_WhenSwitchedToClientSecretJwt_NeedsANewSecret()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateHttpClient(factory);
        var issuer = await GetIssuerAsync(client);

        var clientId = await CreateClientAsync(
            administrator,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );
        var (_, verifiedSecret) = await CreateSecretAsync(administrator, clientId);

        await SetAuthenticationMethodAsync(
            administrator,
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt
        );

        // A verifier is no HMAC key, so the secret issued for client_secret_post cannot sign an assertion.
        Assert.Equal(
            "invalid_client",
            await AuthenticateWithAssertionAsync(client, clientId, verifiedSecret, issuer)
        );

        var (keySecretId, keySecret) = await CreateSecretAsync(administrator, clientId);

        var stored = Assert.Single(
            await factory.GetClientSecretsAsync(clientId),
            secret => secret.SecretId == keySecretId
        );
        Assert.Equal(SecretEncodings.SymmetricRaw, stored.SecretEncoding);
        Assert.Null(await AuthenticateWithAssertionAsync(client, clientId, keySecret, issuer));
    }

    [Fact]
    public async Task PatchSettings_WhenSwitchedAwayFromClientSecretJwt_KeepsTheSecretAndRotatesToAVerifier()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateHttpClient(factory);

        var clientId = await CreateClientAsync(
            administrator,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretJwt
        );
        var (secretId, secretMaterial) = await CreateSecretAsync(administrator, clientId);

        await SetAuthenticationMethodAsync(
            administrator,
            clientId,
            OpenIdProtocol.ClientAuthenticationMethods.ClientSecretPost
        );

        // The secret stays as it was issued, and a presented copy of it still authenticates.
        Assert.Null(await AuthenticateWithSecretAsync(client, clientId, secretMaterial));

        // Its replacement takes the form of the method the client is registered for now.
        var (replacementId, replacementMaterial) = await RotateAsync(
            administrator,
            clientId,
            secretId
        );
        var secrets = await factory.GetClientSecretsAsync(clientId);
        Assert.Equal(
            SecretEncodings.SymmetricRaw,
            Assert.Single(secrets, secret => secret.SecretId == secretId).SecretEncoding
        );
        Assert.Equal(
            SecretEncodings.Verifier,
            Assert.Single(secrets, secret => secret.SecretId == replacementId).SecretEncoding
        );
        Assert.Null(await AuthenticateWithSecretAsync(client, clientId, replacementMaterial));
        Assert.Null(await AuthenticateWithSecretAsync(client, clientId, secretMaterial));
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

    private static async Task<string> GetIssuerAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/.well-known/openid-configuration");
        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        return GetRequiredString(jsonElement, "issuer");
    }

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

    private static async Task SetAuthenticationMethodAsync(
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
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, body);
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

    private static async Task<(string SecretId, string SecretMaterial)> RotateAsync(
        HttpClient administrator,
        string clientId,
        string secretId
    )
    {
        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets/{secretId}/rotate",
            new { expiresWhen = DateTimeOffset.UtcNow.AddYears(1) }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        var jsonElement = JsonElement.Parse(body);
        return (
            GetRequiredString(jsonElement, "secretId"),
            GetRequiredString(jsonElement, "secretMaterial")
        );
    }

    private static string GetRequiredString(JsonElement jsonElement, string propertyName)
    {
        var value = jsonElement.GetProperty(propertyName).GetString();
        Assert.NotNull(value);
        return value;
    }

    // Revoking an unknown token is a no-op once the client authenticates (RFC 7009 §2.2), so the response reports only
    // whether the client was accepted: no error, or the error the endpoint returned.
    private static async Task<string?> RevokeAsync(
        HttpClient client,
        Dictionary<string, string> credentials
    )
    {
        credentials["token"] = "not-a-real-token";
        using var content = new FormUrlEncodedContent(credentials);
        using var response = await client.PostAsync("/oauth2/revoke", content);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            return null;
        }

        var jsonElement = JsonElement.Parse(await response.Content.ReadAsStringAsync());
        return jsonElement.GetProperty("error").GetString();
    }

    private static Task<string?> AuthenticateWithSecretAsync(
        HttpClient client,
        string clientId,
        string clientSecret
    ) =>
        RevokeAsync(
            client,
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
            }
        );

    private static Task<string?> AuthenticateWithAssertionAsync(
        HttpClient client,
        string clientId,
        string clientSecret,
        string audience
    ) =>
        RevokeAsync(
            client,
            new Dictionary<string, string>
            {
                ["client_assertion_type"] = OpenIdProtocol.ClientAssertionTypes.JwtBearer,
                ["client_assertion"] = BuildClientSecretJwt(clientId, clientSecret, audience),
            }
        );

    // A client_secret_jwt assertion (OpenID Connect Core §9): an HS256 JWT keyed by the UTF-8 bytes of the secret.
    private static string BuildClientSecretJwt(
        string clientId,
        string clientSecret,
        string audience
    )
    {
        var now = DateTimeOffset.UtcNow;
        var header = new Dictionary<string, object> { ["alg"] = "HS256", ["typ"] = "JWT" };
        var payload = new Dictionary<string, object>
        {
            ["iss"] = clientId,
            ["sub"] = clientId,
            ["aud"] = audience,
            ["jti"] = Guid.NewGuid().ToString("N"),
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMinutes(5).ToUnixTimeSeconds(),
        };

        var encodedHeader = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(header));
        var encodedPayload = Base64Url.EncodeToString(JsonSerializer.SerializeToUtf8Bytes(payload));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        var signature = HMACSHA256.HashData(
            SecureEncoding.UTF8.GetBytes(clientSecret),
            SecureEncoding.ASCII.GetBytes(signingInput)
        );

        return $"{signingInput}.{Base64Url.EncodeToString(signature)}";
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
