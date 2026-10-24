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
using System.Text;
using System.Text.Json;
using NCode.Identity.OpenId.IntegrationTests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.IntegrationTests.Endpoints;

// A settings write does not fail on a stored client document the settings vocabulary cannot honor: the write skips the
// document, because it is incoherent before and after the change. See ADR-0241.
public class UnreadableSettingsDocumentTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";
    private const string UnreadableClientId = "it-unreadable-client";
    private const string RedirectUri = "http://client.example/callback";

    #region PatchTenantSettings Tests

    [Fact]
    public async Task PatchTenantSettings_WhenAStoredClientDocumentCannotBeHonored_ReturnsNoContent()
    {
        using var factory = CreateFactory();
        using var administrator = await SeedUnreadableClientAsync(factory);

        // access_token_lifetime is a Min setting a client can register, so the change walks the clients.
        var (statusCode, body) = await PatchSettingAsync(
            administrator,
            $"{TenantApiPath}/settings",
            OpenIdSettingNames.AccessTokenLifetime,
            "00:04:00"
        );

        Assert.True(statusCode == HttpStatusCode.NoContent, body);
    }

    #endregion

    #region PatchServerSettings Tests

    [Fact]
    public async Task PatchServerSettings_WhenAStoredClientDocumentCannotBeHonored_ReturnsNoContent()
    {
        using var factory = CreateFactory();
        using var administrator = await SeedUnreadableClientAsync(factory);

        // access_token_lifetime is a Min setting a client can register, so the change walks the clients.
        var (statusCode, body) = await PatchSettingAsync(
            administrator,
            $"/api/servers/{OpenIdServerOptions.DefaultServerId}/settings",
            OpenIdSettingNames.AccessTokenLifetime,
            "00:04:00"
        );

        Assert.True(statusCode == HttpStatusCode.NoContent, body);
    }

    #endregion

    #region Scaffolding

    private static ConfiguredHostApplicationFactory CreateFactory() =>
        new(
            new Dictionary<string, string?>
            {
                ["BootstrapAdmin:ClientId"] = BootstrapClientId,
                ["BootstrapAdmin:ClientSecret"] = Base64Url.EncodeToString(
                    Encoding.UTF8.GetBytes(BootstrapClientSecret)
                ),
                ["BootstrapAdmin:TenantId"] = HostApplicationFactory.TenantId,
            }
        );

    private static async Task<HttpClient> SeedUnreadableClientAsync(HostApplicationFactory factory)
    {
        // A direct store write of a setting no descriptor registers, so no check refuses the client and its document
        // cannot be read.
        await factory.SeedPublicClientWithSettingsAsync(
            UnreadableClientId,
            new Dictionary<string, object> { ["it_retired_setting"] = true },
            RedirectUri
        );
        var administrator = await factory.CreateManagementClientAsync(
            BootstrapClientId,
            BootstrapClientSecret
        );

        // The report proves the document is unreadable before any write is tried.
        using var response = await administrator.GetAsync($"{TenantApiPath}/incoherent-clients");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var item = Assert.Single(
            document.RootElement.GetProperty("items").EnumerateArray(),
            element => element.GetProperty("clientId").GetString() == UnreadableClientId
        );
        Assert.False(string.IsNullOrEmpty(item.GetProperty("documentError").GetString()), body);

        return administrator;
    }

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
                Encoding.UTF8,
                "application/json-patch+json"
            ),
        };

        using var response = await administrator.SendAsync(request);
        return (response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    #endregion
}
