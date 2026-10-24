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

// The management API lists the clients that a tenant stores and that its effective settings make incoherent, through
// the reporter that the server settings reconciliation also uses. See ADR-0233.
public class IncoherentClientReportTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";
    private const string IncoherentClientId = "it-incoherent-client";
    private const string RedirectUri = "http://client.example/callback";

    #region GetIncoherentClients Tests

    [Fact]
    public async Task GetIncoherentClients_WhenAStoredClientsRedirectUriIsNotPermitted_ListsTheClient()
    {
        using var factory = CreateFactory();

        // A direct store write, so no coherence check refuses the client.
        await factory.SeedPublicClientWithSettingsAsync(
            IncoherentClientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.RedirectUriKindsSupported] = new[]
                {
                    OpenIdConstants.RedirectUriKinds.Https,
                },
            },
            RedirectUri
        );
        using var administrator = await factory.CreateManagementClientAsync(
            BootstrapClientId,
            BootstrapClientSecret
        );

        using var response = await administrator.GetAsync($"{TenantApiPath}/incoherent-clients");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToList();
        var item = Assert.Single(
            items,
            element => element.GetProperty("clientId").GetString() == IncoherentClientId
        );
        var violations = item.GetProperty("violations").EnumerateArray().ToList();
        Assert.Contains(
            violations,
            violation =>
                violation.GetProperty("settingName").GetString() == OpenIdSettingNames.RedirectUris
                && violation.GetProperty("governingSettingName").GetString()
                    == OpenIdSettingNames.RedirectUriKindsSupported
        );
        Assert.DoesNotContain(
            items,
            element => element.GetProperty("clientId").GetString() == BootstrapClientId
        );
    }

    [Fact]
    public async Task GetIncoherentClients_WhenAStoredClientsAccessTokenLifetimeExceedsItsTenants_ListsTheClient()
    {
        using var factory = CreateFactory();

        // A direct store write, so no coherence check refuses the client.
        await factory.SeedPublicClientWithSettingsAsync(
            IncoherentClientId,
            new Dictionary<string, object>
            {
                [OpenIdSettingNames.AccessTokenLifetime] = "01:00:00",
            },
            RedirectUri
        );
        using var administrator = await factory.CreateManagementClientAsync(
            BootstrapClientId,
            BootstrapClientSecret
        );

        using var response = await administrator.GetAsync($"{TenantApiPath}/incoherent-clients");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        var items = document.RootElement.GetProperty("items").EnumerateArray().ToList();
        var item = Assert.Single(
            items,
            element => element.GetProperty("clientId").GetString() == IncoherentClientId
        );
        var violations = item.GetProperty("violations").EnumerateArray().ToList();
        Assert.Contains(
            violations,
            violation =>
                violation.GetProperty("settingName").GetString()
                    == OpenIdSettingNames.AccessTokenLifetime
                && violation.GetProperty("governingSettingName").GetString()
                    == OpenIdSettingNames.AccessTokenLifetime
                && violation.GetProperty("value").GetString() == "01:00:00"
        );
    }

    [Fact]
    public async Task GetIncoherentClients_WhenEveryClientIsCoherent_ListsNoClient()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateManagementClientAsync(
            BootstrapClientId,
            BootstrapClientSecret
        );

        using var response = await administrator.GetAsync($"{TenantApiPath}/incoherent-clients");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        using var document = JsonDocument.Parse(body);
        Assert.Empty(document.RootElement.GetProperty("items").EnumerateArray());
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

    #endregion
}
