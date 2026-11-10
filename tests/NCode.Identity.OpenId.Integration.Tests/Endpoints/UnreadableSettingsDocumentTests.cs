// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A settings write does not fail on a stored client document the settings vocabulary cannot honor: the write skips the
// document, because it is incoherent before and after the change. See ADR-1241. A request that needs a stored document
// it cannot honor answers 409 with a problem that names the document. See ADR-1245.
public class UnreadableSettingsDocumentTests
{
    private const string TenantApiPath = $"/api/tenants/{HostApplicationFactory.TenantId}";
    private const string UnreadableClientId = "it-unreadable-client";
    private const string RedirectUri = "http://client.example/callback";

    #region GetTenantEffectiveSettings Tests

    [Fact]
    public async Task GetTenantEffectiveSettings_WhenTheTenantDocumentCannotBeHonored_ReturnsConflictNamingTheTenant()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await MakeTenantDocumentUnreadableAsync(factory);

        using var response = await administrator.GetAsync($"{TenantApiPath}/effective-settings");

        await AssertConflictNamingTheTenantAsync(response);
    }

    #endregion

    #region ListIncoherentClients Tests

    [Fact]
    public async Task ListIncoherentClients_WhenTheTenantDocumentCannotBeHonored_ReturnsConflictNamingTheTenant()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await MakeTenantDocumentUnreadableAsync(factory);

        using var response = await administrator.GetAsync($"{TenantApiPath}/incoherent-clients");

        await AssertConflictNamingTheTenantAsync(response);
    }

    #endregion

    #region PatchTenantSettings Tests

    [Fact]
    public async Task PatchTenantSettings_WhenTheTenantDocumentCannotBeHonored_RepairsIt()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await MakeTenantDocumentUnreadableAsync(factory);

        var (statusCode, body) = await RemoveSettingAsync(
            administrator,
            $"{TenantApiPath}/settings",
            "it_unregistered_setting"
        );

        Assert.True(statusCode == HttpStatusCode.NoContent, body);
        using var response = await administrator.GetAsync($"{TenantApiPath}/effective-settings");
        var effectiveBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, effectiveBody);
    }

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

    #region PatchPlatformSettings Tests

    [Fact]
    public async Task PatchPlatformSettings_WhenAStoredClientDocumentCannotBeHonored_ReturnsNoContent()
    {
        using var factory = CreateFactory();
        using var administrator = await SeedUnreadableClientAsync(factory);

        // access_token_lifetime is a Min setting a client can register, so the change walks the clients.
        var (statusCode, body) = await PatchSettingAsync(
            administrator,
            "/api/platform/settings",
            OpenIdSettingNames.AccessTokenLifetime,
            "00:04:00"
        );

        Assert.True(statusCode == HttpStatusCode.NoContent, body);
    }

    #endregion

    #region ResetLocalAccountPassword Tests

    [Fact]
    public async Task ResetLocalAccountPassword_WhenTheTenantDocumentCannotBeHonored_ReturnsConflictNamingTheTenant()
    {
        using var factory = CreateFactory();
        var localAccountId = await factory.SeedLocalAccountAsync(
            "it-unreadable-user",
            "unreadable-settings-password",
            CancellationToken.None
        );
        using var administrator = await factory.CreateAdministratorClientAsync(
            CancellationToken.None
        );
        await MakeTenantDocumentUnreadableAsync(factory);

        using var response = await administrator.PostAsync(
            $"{TenantApiPath}/local-accounts/{localAccountId}/password",
            JsonContent.Create(new { password = "unreadable-settings-password-reset" })
        );

        await AssertConflictNamingTheTenantAsync(response);
    }

    #endregion

    #region Scaffolding

    private static BootstrapAdminApplicationFactory CreateFactory() => new();

    private static async Task<HttpClient> SeedUnreadableClientAsync(
        BootstrapAdminApplicationFactory factory
    )
    {
        // A direct store write of a setting no descriptor registers, so no check refuses the client and its document
        // cannot be read.
        await factory.SeedPublicClientWithSettingsAsync(
            UnreadableClientId,
            new Dictionary<string, object> { ["it_unregistered_setting"] = true },
            [RedirectUri],
            CancellationToken.None
        );
        var administrator = await factory.CreateAdministratorClientAsync(CancellationToken.None);

        // The report proves the document is unreadable before any write is tried.
        using var response = await administrator.GetAsync($"{TenantApiPath}/incoherent-clients");

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);
        var jsonElement = JsonElement.Parse(body);
        var item = Assert.Single(
            jsonElement.GetProperty("items").EnumerateArray(),
            element => element.GetProperty("clientId").GetString() == UnreadableClientId
        );
        Assert.False(string.IsNullOrEmpty(item.GetProperty("documentError").GetString()), body);

        return administrator;
    }

    private static ValueTask MakeTenantDocumentUnreadableAsync(HostApplicationFactory factory) =>
        // A direct store write of a setting no descriptor registers, after the management client has authenticated, so
        // the tenant the host materialized stays cached and the management request reads the stored document.
        factory.ReplaceTenantSettingsAsync(
            new Dictionary<string, object> { ["it_unregistered_setting"] = true },
            CancellationToken.None
        );

    private static async Task AssertConflictNamingTheTenantAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Conflict, body);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var jsonElement = JsonElement.Parse(body);
        var problem = jsonElement;
        Assert.Equal(OpenIdSettingScopes.Tenant, problem.GetProperty("settingsScope").GetString());
        Assert.Equal(HostApplicationFactory.TenantId, problem.GetProperty("ownerId").GetString());
        Assert.Contains(
            "it_unregistered_setting",
            problem.GetProperty("documentError").GetString()
        );
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
