// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Identity.Server.SecretProtection;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A rekey retires the active master key and creates the next version, the values the retired key protected are
// rewrapped under the new one, and everything keeps working (ADR-1407).
public class MasterKeyRotationTests
{
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";

    [Fact]
    public async Task Rekey_OfATenant_RewrapsItsSecretsAndTheClientStillAuthenticates()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        using var client = CreateHttpClient(factory);
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
            rotation.GetProperty("activeKey").GetProperty("keyId").GetString()
                ?? throw new InvalidOperationException("The response has no activeKey.keyId.")
        );
        Assert.Equal(2, rotation.GetProperty("activeKey").GetProperty("version").GetInt32());
        Assert.Equal("active", rotation.GetProperty("activeKey").GetProperty("state").GetString());
        Assert.Equal(1, rotation.GetProperty("retiredKey").GetProperty("version").GetInt32());
        Assert.Equal(
            "retired",
            rotation.GetProperty("retiredKey").GetProperty("state").GetString()
        );

        // Every replica has not loaded the new key yet, so a second rekey waits.
        using var tooSoon = await administrator.PostAsync(
            $"{TenantApiPath}/encryption-keys/rekey",
            content: null
        );
        Assert.Equal(HttpStatusCode.Conflict, tooSoon.StatusCode);
        var retryAfter = tooSoon.Headers.RetryAfter;
        Assert.NotNull(retryAfter);
        var retryAfterDelta = retryAfter.Delta;
        Assert.NotNull(retryAfterDelta);
        Assert.True(retryAfterDelta.Value > TimeSpan.Zero);
        var tooSoonProblem = JsonElement.Parse(await tooSoon.Content.ReadAsStringAsync());
        Assert.Equal(
            (long)retryAfterDelta.Value.TotalSeconds,
            tooSoonProblem.GetProperty("retryAfterSeconds").GetInt64()
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
        var revocationErrorBeforeRewrap = await ClientAuthenticationProbe.RevokeWithSecretAsync(
            client,
            clientId,
            secretMaterial,
            CancellationToken.None
        );
        Assert.Null(revocationErrorBeforeRewrap);

        using var scope = factory.Services.CreateScope();
        var manager = scope.ServiceProvider.GetRequiredService<IMasterKeyManager>();
        var provider = scope.ServiceProvider.GetRequiredService<ISecretProtectorProvider>();
        var rewrapped = await manager.RewrapAsync(
            SecretOwner.ForTenant(HostApplicationFactory.TenantId),
            CancellationToken.None
        );

        Assert.True(rewrapped.Rewrapped >= 1);
        Assert.Equal(0, rewrapped.Failed);
        var clientSecrets = await factory.GetClientSecretsAsync(clientId, CancellationToken.None);
        var stored = Assert.Single(clientSecrets);
        Assert.True(provider.TryReadMasterKeyId(stored.EncodedValue, out var storedKeyId));
        Assert.Equal(activeKeyId, storedKeyId);
        var revocationErrorAfterRewrap = await ClientAuthenticationProbe.RevokeWithSecretAsync(
            client,
            clientId,
            secretMaterial,
            CancellationToken.None
        );
        Assert.Null(revocationErrorAfterRewrap);
    }

    [Fact]
    public async Task Rekey_OfAnUnknownTenant_ReturnsNotFoundAndCreatesNoKey()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        using var response = await administrator.PostAsync(
            "/api/tenants/no-such-tenant/encryption-keys/rekey",
            content: null
        );

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        using var scope = factory.Services.CreateScope();
        var storeManagerFactory = scope.ServiceProvider.GetRequiredService<IStoreManagerFactory>();
        await using var storeManager = await storeManagerFactory.CreateAsync(
            CancellationToken.None
        );
        var rows = await storeManager
            .GetStore<IMasterKeyStore>()
            .ListAsync(SecretOwner.ForTenant("no-such-tenant").Normalize(), CancellationToken.None);
        Assert.Empty(rows);
    }

    [Fact]
    public async Task Rekey_OfThePlatform_KeepsTheHostServingTheManagementApi()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );

        // The discovery request materializes the platform, so the store holds it.
        using var discovery = await CreateHttpClient(factory)
            .GetAsync("/.well-known/openid-configuration");
        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);

        const string platformPath = "/api/platform";
        var before = await GetKeysAsync(administrator, platformPath);
        Assert.Equal(1, before.GetProperty("keys").GetArrayLength());

        using var rekey = await administrator.PostAsync(
            $"{platformPath}/encryption-keys/rekey",
            content: null
        );
        var rekeyBody = await rekey.Content.ReadAsStringAsync();
        Assert.True(rekey.StatusCode == HttpStatusCode.OK, rekeyBody);
        Assert.Equal(
            2,
            JsonElement.Parse(rekeyBody).GetProperty("activeKey").GetProperty("version").GetInt32()
        );

        var after = await GetKeysAsync(administrator, platformPath);
        Assert.Equal(2, after.GetProperty("keys").GetArrayLength());

        // The management API still answers, and so does the tenant plane.
        using var tenants = await administrator.GetAsync($"{TenantApiPath}/clients?limit=1");
        Assert.Equal(HttpStatusCode.OK, tenants.StatusCode);
    }

    [Fact]
    public async Task SecretProtectionCommand_DryRun_VerifiesEveryMasterKeyAndReturnsZero()
    {
        using var factory = new BootstrapAdminApplicationFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        var clientId = await CreateClientAsync(administrator);
        await CreateSecretAsync(administrator, clientId);

        var command = SecretProtectionCommandLine.Parse([
            "secret-protection",
            "rewrap",
            "--dry-run",
        ]);
        Assert.NotNull(command);
        Assert.Null(command.Error);

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
        return JsonElement.Parse(body).GetProperty("clientId").GetString()
            ?? throw new InvalidOperationException("The response has no clientId.");
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
        return JsonElement.Parse(body).GetProperty("secretMaterial").GetString()
            ?? throw new InvalidOperationException("The response has no secretMaterial.");
    }

    #endregion
}
