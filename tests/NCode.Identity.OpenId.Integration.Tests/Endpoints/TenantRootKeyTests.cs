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
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.Auditing;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A tenant brings a root key: its master keys are wrapped under the key and no other copy exists, a revoked key makes
// the tenant fail closed, and a restored or removed key brings the tenant back (ADR-1414).
public class TenantRootKeyTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string ManagementScope = "read:clients";
    private const string ControlPlaneTenantId = "root";
    private const string TenantId = "acme";
    private const string TenantApiPath = $"/api/tenants/{TenantId}";
    private const string RootKeyPath = $"{TenantApiPath}/encryption-keys/root-key";
    private const string KeyReference = $"{TestReferencedKeyWrapper.ReferencePrefix}one";

    [Fact]
    public async Task RootKey_WhenSetRevokedRestoredAndRemoved_FailsClosedAndRecovers()
    {
        var wrapper = new TestReferencedKeyWrapper();
        var recorder = new RecordingAuditEventHandler();
        using var factory = new TenantRootKeyHostApplicationFactory(wrapper, recorder);
        using var administrator = await CreateAdministratorClientAsync(factory);
        using var client = CreateHttpClient(factory);
        await CreateTenantAsync(administrator);
        var clientId = await CreateClientAsync(administrator);
        var secretMaterial = await CreateSecretAsync(administrator, clientId);

        var before = await GetKeysAsync(administrator);
        Assert.Contains(
            TestReferencedKeyWrapper.WrapperName,
            before.GetProperty("rootKeyProviders").EnumerateArray().Select(e => e.GetString())
        );
        Assert.All(
            before.GetProperty("keys").EnumerateArray(),
            key => Assert.Null(GetString(key, "rootKeyReference"))
        );

        // Setting the key wraps every master key under it, with no recovery copy.
        using var set = await PutRootKeyAsync(administrator, KeyReference);
        var setBody = await set.Content.ReadAsStringAsync();
        Assert.True(set.StatusCode == HttpStatusCode.OK, setBody);
        var setResult = JsonElement.Parse(setBody);
        Assert.Equal("active", setResult.GetProperty("rootKey").GetProperty("state").GetString());
        Assert.Equal(0, setResult.GetProperty("failedKeys").GetInt32());

        var underKey = await GetKeysAsync(administrator);
        Assert.Equal("active", underKey.GetProperty("rootKey").GetProperty("state").GetString());
        Assert.Equal(
            KeyReference,
            underKey.GetProperty("rootKey").GetProperty("keyReference").GetString()
        );
        var keys = underKey.GetProperty("keys").EnumerateArray().ToList();
        Assert.Equal(2, keys.Count);
        Assert.All(
            keys,
            key =>
            {
                Assert.Equal(KeyReference, GetString(key, "rootKeyReference"));
                Assert.Equal(
                    TestReferencedKeyWrapper.WrapperName,
                    GetString(key, "rootKeyProvider")
                );
                Assert.Null(GetString(key, "recoveryKeyId"));
            }
        );
        Assert.Contains(
            recorder.Events.OfType<RootKeyAuditEvent>(),
            auditEvent =>
                auditEvent.Change == RootKeyChanges.Set
                && auditEvent.KeyReference == KeyReference
                && auditEvent.Reason == RootKeyChangeReasons.Requested
        );

        // A client that existed before still authenticates: its secret opens through the tenant's key.
        Assert.Null(await AuthenticateClientAsync(client, clientId, secretMaterial));

        // The tenant revokes the key, and the next validation makes the tenant fail closed.
        wrapper.Revoke(KeyReference);
        using var revoked = await administrator.PostAsync($"{RootKeyPath}/validate", content: null);
        var revokedBody = await revoked.Content.ReadAsStringAsync();
        Assert.True(revoked.StatusCode == HttpStatusCode.OK, revokedBody);
        var revokedKey = JsonElement.Parse(revokedBody);
        Assert.Equal("unavailable", revokedKey.GetProperty("state").GetString());
        Assert.NotNull(GetString(revokedKey, "failure"));
        Assert.Contains(
            recorder.Events.OfType<RootKeyAuditEvent>(),
            auditEvent =>
                auditEvent.Change == RootKeyChanges.Unavailable
                && auditEvent.KeyReference == KeyReference
                && auditEvent.Reason == RootKeyChangeReasons.ValidationFailed
        );

        // A call that needs the tenant's protector is refused, and asks the caller to retry.
        using var refused = await PostSecretAsync(administrator, clientId);
        Assert.True(
            refused.StatusCode == HttpStatusCode.ServiceUnavailable,
            await refused.Content.ReadAsStringAsync()
        );
        Assert.True(refused.Headers.RetryAfter?.Delta > TimeSpan.Zero);

        // The tenant restores the key, and the next validation brings the tenant back.
        wrapper.Restore(KeyReference);
        using var restored = await administrator.PostAsync(
            $"{RootKeyPath}/validate",
            content: null
        );
        var restoredBody = await restored.Content.ReadAsStringAsync();
        Assert.True(restored.StatusCode == HttpStatusCode.OK, restoredBody);
        Assert.Equal("active", JsonElement.Parse(restoredBody).GetProperty("state").GetString());
        Assert.Contains(
            recorder.Events.OfType<RootKeyAuditEvent>(),
            auditEvent =>
                auditEvent.Change == RootKeyChanges.Restored
                && auditEvent.KeyReference == KeyReference
        );
        Assert.Null(await AuthenticateClientAsync(client, clientId, secretMaterial));

        // Removing the key moves every master key back under the deployment's root key.
        using var removed = await administrator.DeleteAsync(RootKeyPath);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var afterRemoval = await GetKeysAsync(administrator);
        Assert.Null(GetOptionalObject(afterRemoval, "rootKey"));
        Assert.Equal(2, afterRemoval.GetProperty("keys").GetArrayLength());
        Assert.All(
            afterRemoval.GetProperty("keys").EnumerateArray(),
            key =>
            {
                Assert.Null(GetString(key, "rootKeyReference"));
                Assert.NotEqual(
                    TestReferencedKeyWrapper.WrapperName,
                    GetString(key, "rootKeyProvider")
                );
            }
        );
        Assert.Contains(
            recorder.Events.OfType<RootKeyAuditEvent>(),
            auditEvent => auditEvent.Change == RootKeyChanges.Removed
        );
        Assert.Null(await AuthenticateClientAsync(client, clientId, secretMaterial));
    }

    [Fact]
    public async Task PutRootKey_WithAProviderThatIsNotListed_ReturnsBadRequestAndSetsNoKey()
    {
        using var factory = new TenantRootKeyHostApplicationFactory(
            new TestReferencedKeyWrapper(),
            new RecordingAuditEventHandler()
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTenantAsync(administrator);

        using var response = await administrator.PutAsJsonAsync(
            RootKeyPath,
            new { provider = "aws-kms", keyReference = KeyReference }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.True(
            JsonElement.Parse(body).GetProperty("errors").TryGetProperty("provider", out _)
        );

        var keys = await GetKeysAsync(administrator);
        Assert.Null(GetOptionalObject(keys, "rootKey"));
    }

    [Fact]
    public async Task PutRootKey_WithinTwiceTheRefreshIntervalOfARekey_ReturnsConflictAndSetsNoKey()
    {
        using var factory = new TenantRootKeyHostApplicationFactory(
            new TestReferencedKeyWrapper(),
            new RecordingAuditEventHandler()
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        await CreateTenantAsync(administrator);
        var clientId = await CreateClientAsync(administrator);
        await CreateSecretAsync(administrator, clientId);

        using var rekey = await administrator.PostAsync(
            $"{TenantApiPath}/encryption-keys/rekey",
            content: null
        );
        var rekeyBody = await rekey.Content.ReadAsStringAsync();
        Assert.True(rekey.StatusCode == HttpStatusCode.OK, rekeyBody);

        using var response = await PutRootKeyAsync(administrator, KeyReference);

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
        Assert.True(response.Headers.RetryAfter?.Delta > TimeSpan.Zero);

        var keys = await GetKeysAsync(administrator);
        Assert.Null(GetOptionalObject(keys, "rootKey"));
        Assert.All(
            keys.GetProperty("keys").EnumerateArray(),
            key => Assert.Null(GetString(key, "rootKeyReference"))
        );
    }

    [Fact]
    public async Task PutRootKey_OnTheControlPlaneTenant_ReturnsBadRequestAndReportsNoProviders()
    {
        using var factory = new TenantRootKeyHostApplicationFactory(
            new TestReferencedKeyWrapper(),
            new RecordingAuditEventHandler()
        );
        using var administrator = await CreateAdministratorClientAsync(factory);
        var controlPlaneRootKeyPath =
            $"/api/tenants/{ControlPlaneTenantId}/encryption-keys/root-key";

        using var response = await administrator.PutAsJsonAsync(
            controlPlaneRootKeyPath,
            new { provider = TestReferencedKeyWrapper.WrapperName, keyReference = KeyReference }
        );

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.BadRequest, body);
        Assert.Equal(
            "The tenant cannot bring a root key.",
            JsonElement.Parse(body).GetProperty("title").GetString()
        );

        using var keysResponse = await administrator.GetAsync(
            $"/api/tenants/{ControlPlaneTenantId}/encryption-keys"
        );
        var keysBody = await keysResponse.Content.ReadAsStringAsync();
        Assert.True(keysResponse.StatusCode == HttpStatusCode.OK, keysBody);
        var keys = JsonElement.Parse(keysBody);
        Assert.Empty(keys.GetProperty("rootKeyProviders").EnumerateArray());
        Assert.Null(GetOptionalObject(keys, "rootKey"));
    }

    #region Scaffolding

    private static HttpClient CreateHttpClient(HostApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    private static async Task CreateTenantAsync(HttpClient administrator)
    {
        using var response = await administrator.PostAsJsonAsync(
            $"/api/teams/{HostApplicationFactory.TeamId}/tenants",
            new
            {
                tenantId = TenantId,
                displayName = TenantId,
                isDisabled = false,
                settings = new { },
            }
        );
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
    }

    // Revokes an unknown token at the tenant's route: revocation is a no-op once the client authenticates, so the
    // response reports only whether the client was accepted.
    private static async Task<string?> AuthenticateClientAsync(
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
        using var response = await client.PostAsync($"/{TenantId}/oauth2/revoke", content);
        if (response.StatusCode == HttpStatusCode.OK)
            return null;

        return JsonElement
            .Parse(await response.Content.ReadAsStringAsync())
            .GetProperty("error")
            .GetString();
    }

    private static string? GetString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;

    private static JsonElement? GetOptionalObject(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind != JsonValueKind.Null
            ? value
            : null;

    private static async Task<JsonElement> GetKeysAsync(HttpClient administrator)
    {
        using var response = await administrator.GetAsync($"{TenantApiPath}/encryption-keys");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        return JsonElement.Parse(body);
    }

    private static Task<HttpResponseMessage> PutRootKeyAsync(
        HttpClient administrator,
        string keyReference
    ) =>
        administrator.PutAsJsonAsync(
            RootKeyPath,
            new { provider = TestReferencedKeyWrapper.WrapperName, keyReference }
        );

    private static async Task<HttpClient> CreateAdministratorClientAsync(
        HostApplicationFactory factory
    )
    {
        var client = CreateHttpClient(factory);
        var credentials = SecureEncoding.UTF8.GetBytes(
            $"{BootstrapClientId}:{BootstrapClientSecret}"
        );
        using var tokenRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/{ControlPlaneTenantId}/oauth2/token"
        )
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
        return JsonElement.Parse(body).GetProperty("clientId").GetString()
            ?? throw new InvalidOperationException("The response has no clientId.");
    }

    private static Task<HttpResponseMessage> PostSecretAsync(
        HttpClient administrator,
        string clientId
    ) =>
        administrator.PostAsJsonAsync(
            $"{TenantApiPath}/clients/{clientId}/secrets",
            new
            {
                secretType = "symmetric",
                keySizeBits = 256,
                expiresWhen = DateTimeOffset.UtcNow.AddYears(1),
            }
        );

    private static async Task<string> CreateSecretAsync(HttpClient administrator, string clientId)
    {
        using var response = await PostSecretAsync(administrator, clientId);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created, body);
        return JsonElement.Parse(body).GetProperty("secretMaterial").GetString()
            ?? throw new InvalidOperationException("The response has no secretMaterial.");
    }

    /// <summary>
    /// A factory that selects the tenant by path, with the bootstrap administrator in the control-plane tenant,
    /// <see cref="TestReferencedKeyWrapper"/> registered and
    /// allowed as the key wrapper of an owner's root key, and an audit recorder registered.
    /// </summary>
    private sealed class TenantRootKeyHostApplicationFactory(
        TestReferencedKeyWrapper wrapper,
        RecordingAuditEventHandler recorder
    ) : HostApplicationFactory
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
                        ["BootstrapAdmin:TenantId"] = ControlPlaneTenantId,
                        ["OpenId:TenantResolution:StrategyCode"] = OpenIdConstants
                            .TenantStrategyCodes
                            .DynamicByPath,
                        ["OpenId:TenantResolution:DynamicByPath:IssuerTemplate"] =
                            "http://localhost/{tenantId}",
                        ["SecretProtection:OwnerRootKeys:Providers:0"] =
                            TestReferencedKeyWrapper.WrapperName,
                    }
                )
            );
            builder.ConfigureServices(services =>
            {
                services.AddSingleton<IKeyWrapper>(wrapper);
                services.AddSingleton<IEventHandler<IAuditEvent>>(recorder);
            });
        }
    }

    #endregion
}
