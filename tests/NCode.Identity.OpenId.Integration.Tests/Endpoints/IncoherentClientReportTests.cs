// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Net;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// The management API lists the clients that a tenant stores and that its effective settings make incoherent, through
// the reporter that the server settings reconciliation also uses. See ADR-1233.
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
        var jsonElement = JsonElement.Parse(body);
        var items = jsonElement.GetProperty("items").EnumerateArray().ToList();
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
        var jsonElement = JsonElement.Parse(body);
        var items = jsonElement.GetProperty("items").EnumerateArray().ToList();
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
        var jsonElement = JsonElement.Parse(body);
        Assert.Empty(jsonElement.GetProperty("items").EnumerateArray());
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

    #endregion
}
