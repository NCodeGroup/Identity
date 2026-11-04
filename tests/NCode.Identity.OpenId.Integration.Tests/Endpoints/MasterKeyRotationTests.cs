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
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.SecretProtection;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A rekey retires the active master key and creates the next version, the values the retired key protected are
// rewrapped under the new one, and everything keeps working (ADR-1407).
public class MasterKeyRotationTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string ManagementScope = "read:clients";
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";

    [Fact]
    public async Task Rekey_OfATenant_RewrapsItsSecretsAndTheClientStillAuthenticates()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var client = CreateHttpClient(factory);
        var clientId = await CreateClientAsync(administrator);
        var secretMaterial = await CreateSecretAsync(administrator, clientId);

        var before = await GetKeysAsync(administrator, TenantApiPath);
        var firstKey = Assert.Single(before.GetProperty("keys").EnumerateArray());
        Assert.Equal("active", firstKey.GetProperty("state").GetString());
        Assert.Equal(1, firstKey.GetProperty("version").GetInt32());

        using var rekey = await administrator.PostAsync(
            $"{TenantApiPath}/encryption-keys/rekey",
            content: null
        );
        var rekeyBody = await rekey.Content.ReadAsStringAsync();
        Assert.True(rekey.StatusCode == HttpStatusCode.OK, rekeyBody);
        var rotation = JsonElement.Parse(rekeyBody);
        var activeKeyId = Guid.Parse(
            rotation.GetProperty("activeKey").GetProperty("keyId").GetString()!
        );
        Assert.Equal(2, rotation.GetProperty("activeKey").GetProperty("version").GetInt32());
        Assert.Equal("active", rotation.GetProperty("activeKey").GetProperty("state").GetString());
        Assert.Equal(1, rotation.GetProperty("retiredKey").GetProperty("version").GetInt32());
        Assert.Equal(
            "retired",
            rotation.GetProperty("retiredKey").GetProperty("state").GetString()
        );

        // The status never carries material.
        var after = await GetKeysAsync(administrator, TenantApiPath);
        Assert.Equal(2, after.GetProperty("keys").GetArrayLength());
        foreach (var key in after.GetProperty("keys").EnumerateArray())
        {
            Assert.False(key.TryGetProperty("rootWrappedKey", out _));
            Assert.False(key.TryGetProperty("keyCheck", out _));
        }

        // The retired key still opens what it protected, before and after the rewrap.
        Assert.Null(
            await ClientAuthenticationProbe.RevokeWithSecretAsync(client, clientId, secretMaterial)
        );

        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IMasterKeyManager>();
        var provider = scope.ServiceProvider.GetRequiredService<ISecretProtectorProvider>();
        var rewrapped = await manager.RewrapAsync(
            SecretOwner.ForTenant(HostApplicationFactory.TenantId),
            CancellationToken.None
        );

        Assert.True(rewrapped >= 1);
        var stored = Assert.Single(await factory.GetClientSecretsAsync(clientId));
        Assert.True(provider.TryReadMasterKeyId(stored.EncodedValue, out var storedKeyId));
        Assert.Equal(activeKeyId, storedKeyId);
        Assert.Null(
            await ClientAuthenticationProbe.RevokeWithSecretAsync(client, clientId, secretMaterial)
        );
    }

    [Fact]
    public async Task Rekey_OfAnUnknownTenant_ReturnsNotFoundAndCreatesNoKey()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);

        using var response = await administrator.PostAsync(
            "/api/tenants/no-such-tenant/encryption-keys/rekey",
            content: null
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Rekey_OfTheServer_KeepsTheHostServingTheManagementApi()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);

        // The discovery request materializes the server, so the store holds it.
        using var discovery = await CreateHttpClient(factory)
            .GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);

        var serverId = OpenIdServerOptions.DefaultServerId;
        var serverPath = $"/api/servers/{serverId}";
        var before = await GetKeysAsync(administrator, serverPath);
        Assert.Equal(1, before.GetProperty("keys").GetArrayLength());

        using var rekey = await administrator.PostAsync(
            $"{serverPath}/encryption-keys/rekey",
            content: null
        );
        var rekeyBody = await rekey.Content.ReadAsStringAsync();
        Assert.True(rekey.StatusCode == HttpStatusCode.OK, rekeyBody);
        Assert.Equal(
            2,
            JsonElement.Parse(rekeyBody).GetProperty("activeKey").GetProperty("version").GetInt32()
        );

        var after = await GetKeysAsync(administrator, serverPath);
        Assert.Equal(2, after.GetProperty("keys").GetArrayLength());

        // The management API still answers, and so does the tenant plane.
        using var tenants = await administrator.GetAsync($"{TenantApiPath}/clients?limit=1");
        Assert.Equal(HttpStatusCode.OK, tenants.StatusCode);
    }

    [Fact]
    public async Task SecretProtectionCommand_DryRun_VerifiesEveryMasterKeyAndReturnsZero()
    {
        using var factory = new BootstrapAdminHostApplicationFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var clientId = await CreateClientAsync(administrator);
        await CreateSecretAsync(administrator, clientId);

        Assert.True(
            SecretProtectionCommandLine.TryParse(
                ["secret-protection", "rewrap", "--dry-run"],
                out var command,
                out var error
            ),
            error
        );
        Assert.NotNull(command);

        var exitCode = await command.RunAsync(factory.Services, CancellationToken.None);

        Assert.Equal(0, exitCode);
    }

    #region Scaffolding

    private static HttpClient CreateHttpClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task<JsonElement> GetKeysAsync(HttpClient administrator, string ownerPath)
    {
        using var response = await administrator.GetAsync($"{ownerPath}/encryption-keys");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonElement.Parse(body);
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
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            JsonElement.Parse(body).GetProperty("access_token").GetString()
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
        return JsonElement.Parse(body).GetProperty("clientId").GetString()!;
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
        return JsonElement.Parse(body).GetProperty("secretMaterial").GetString()!;
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
