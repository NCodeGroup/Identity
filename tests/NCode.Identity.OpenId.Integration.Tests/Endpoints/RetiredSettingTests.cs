// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A retired setting stays registered until a later release removes it: a stored document that carries it loads, an
// update may keep or remove it, and a write that adds it is refused. See ADR-1251.
public class RetiredSettingTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";
    private const string RetiredSetting = "mtls_endpoint_alias_authority";
    private const string RetiredValue = "https://mtls.example.test";

    #region Discovery Tests

    [Fact]
    public async Task Discovery_WhenTheTenantDocumentCarriesARetiredSetting_ServesTheTenant()
    {
        using var factory = new HostApplicationFactory();
        await factory.SeedTenantSettingsAsync(
            new Dictionary<string, object> { [RetiredSetting] = RetiredValue }
        );
        var client = factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );

        using var response = await client.GetAsync("/.well-known/openid-configuration");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        Assert.DoesNotContain(RetiredValue, body, StringComparison.Ordinal);
    }

    #endregion

    #region PatchTenantSettings Tests

    [Fact]
    public async Task PatchTenantSettings_WhenTheStoredDocumentCarriesARetiredSetting_KeepsIt()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateManagementClientAsync(
            BootstrapClientId,
            BootstrapClientSecret
        );
        await factory.ReplaceTenantSettingsAsync(
            new Dictionary<string, object> { [RetiredSetting] = RetiredValue }
        );

        var (statusCode, body) = await PatchSettingAsync(
            administrator,
            $"{TenantApiPath}/settings",
            OpenIdSettingNames.AccessTokenLifetime,
            "00:04:00"
        );

        Assert.True(statusCode == HttpStatusCode.NoContent, body);
        using var response = await administrator.GetAsync($"{TenantApiPath}/settings");
        var settingsBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, settingsBody);
        var jsonElement = JsonElement.Parse(settingsBody);
        var settings = jsonElement.GetProperty("settings");
        Assert.Equal(RetiredValue, settings.GetProperty(RetiredSetting).GetString());
    }

    [Fact]
    public async Task PatchTenantSettings_WhenTheChangeAddsARetiredSetting_ReturnsBadRequest()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateManagementClientAsync(
            BootstrapClientId,
            BootstrapClientSecret
        );

        var (statusCode, body) = await PatchSettingAsync(
            administrator,
            $"{TenantApiPath}/settings",
            RetiredSetting,
            RetiredValue
        );

        Assert.True(statusCode == HttpStatusCode.BadRequest, body);
        Assert.Contains("is retired", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PatchTenantSettings_WhenTheChangeRemovesARetiredSetting_RemovesIt()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateManagementClientAsync(
            BootstrapClientId,
            BootstrapClientSecret
        );
        await factory.ReplaceTenantSettingsAsync(
            new Dictionary<string, object> { [RetiredSetting] = RetiredValue }
        );

        var (statusCode, body) = await RemoveSettingAsync(
            administrator,
            $"{TenantApiPath}/settings",
            RetiredSetting
        );

        Assert.True(statusCode == HttpStatusCode.NoContent, body);
        using var response = await administrator.GetAsync($"{TenantApiPath}/settings");
        var settingsBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, settingsBody);
        var jsonElement = JsonElement.Parse(settingsBody);
        var settings = jsonElement.GetProperty("settings");
        Assert.False(settings.TryGetProperty(RetiredSetting, out _), settingsBody);
    }

    #endregion

    #region Scaffolding

    private static ConfiguredHostApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                    SecureEncoding.UTF8.GetBytes(BootstrapClientSecret)
                ),
                ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
            }
        );

    private static async Task<(HttpStatusCode StatusCode, string Body)> PatchSettingAsync(
        HttpClient administrator,
        string path,
        string settingName,
        object value
    )
    {
        var operations = new[]
        {
            new
            {
                op = "add",
                path = $"/{settingName}",
                value = value,
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

    private static async Task<(HttpStatusCode StatusCode, string Body)> RemoveSettingAsync(
        HttpClient administrator,
        string path,
        string settingName
    )
    {
        var operations = new[] { new { op = "remove", path = $"/{settingName}" } };
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

    #endregion
}
