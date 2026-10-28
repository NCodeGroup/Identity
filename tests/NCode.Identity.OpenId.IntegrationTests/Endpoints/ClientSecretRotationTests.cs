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
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using NCode.Buffers;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// Rotating a client secret through the management API replaces it in one change: the replacement authenticates the
// client at once and the rotated secret stops (ADR-0183).
public class ClientSecretRotationTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string ManagementScope = "read:clients";
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";

    [Fact]
    public async Task PostRotate_WhenSecretIsShared_ReplacesItAndRevealsTheReplacementOnce()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateHttpClient(factory);

        var clientId = await CreateClientAsync(administrator);
        var (secretId, secretMaterial) = await CreateSecretAsync(administrator, clientId);
        Assert.Null(await AuthenticateAsync(client, clientId, secretMaterial));

        using var rotateResponse = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets/{secretId}/rotate",
            new { expiresWhen = DateTimeOffset.UtcNow.AddYears(1) }
        );
        var rotateBody = await rotateResponse.Content.ReadAsStringAsync();
        Assert.True(rotateResponse.StatusCode == HttpStatusCode.Created, rotateBody);

        using var rotated = JsonDocument.Parse(rotateBody);
        var replacementId = rotated.RootElement.GetProperty("secretId").GetString()!;
        var replacementMaterial = rotated.RootElement.GetProperty("secretMaterial").GetString()!;
        Assert.NotEqual(secretId, replacementId);
        Assert.False(string.IsNullOrEmpty(replacementMaterial));
        Assert.Equal("symmetric", rotated.RootElement.GetProperty("secretType").GetString());

        Assert.Null(await AuthenticateAsync(client, clientId, replacementMaterial));
        Assert.Equal("invalid_client", await AuthenticateAsync(client, clientId, secretMaterial));

        // The replacement is listed without its material; the rotated secret is gone.
        using var listResponse = await administrator.GetAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets"
        );
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.True(listResponse.StatusCode == HttpStatusCode.OK, listBody);
        using var list = JsonDocument.Parse(listBody);
        var secret = Assert.Single(list.RootElement.GetProperty("secrets").EnumerateArray());
        Assert.Equal(replacementId, secret.GetProperty("secretId").GetString());
        Assert.False(secret.TryGetProperty("secretMaterial", out _));
    }

    [Fact]
    public async Task PostRotate_WhenSecretIsUnknown_ReturnsNotFound()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var clientId = await CreateClientAsync(administrator);

        using var response = await administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets/unknown-secret/rotate",
            new { expiresWhen = DateTimeOffset.UtcNow.AddYears(1) }
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
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
        using var document = JsonDocument.Parse(body);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            document.RootElement.GetProperty("access_token").GetString()
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
                    ["token_endpoint_auth_method"] = OpenIdConstants
                        .ClientAuthenticationMethods
                        .ClientSecretPost,
                },
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.GetProperty("clientId").GetString()!;
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
        using var document = JsonDocument.Parse(body);
        return (
            document.RootElement.GetProperty("secretId").GetString()!,
            document.RootElement.GetProperty("secretMaterial").GetString()!
        );
    }

    // Revoking an unknown token is a no-op once the client authenticates (RFC 7009 §2.2), so the response reports only
    // whether the client secret was accepted: no error, or the error the endpoint returned.
    private static async Task<string?> AuthenticateAsync(
        HttpClient client,
        string clientId,
        string clientSecret
    )
    {
        using var content = new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["token"] = "not-a-real-token",
            }
        );
        using var response = await client.PostAsync("/oauth2/revoke", content);
        if (response.StatusCode == HttpStatusCode.OK)
        {
            return null;
        }

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("error").GetString();
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
