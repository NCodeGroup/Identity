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
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using NCode.Buffers;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// A retired setting stays registered until a later release removes it: a stored document that carries it loads, an
// update may keep or remove it, and a write that adds it is refused. See ADR-0251.
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
        var document = JsonElement.Parse(settingsBody);
        var settings = document.GetProperty("settings");
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
        var document = JsonElement.Parse(settingsBody);
        var settings = document.GetProperty("settings");
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
