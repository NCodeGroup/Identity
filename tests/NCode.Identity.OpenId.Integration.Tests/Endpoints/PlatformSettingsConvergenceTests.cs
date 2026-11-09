// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Text.Json;
using NCode.Buffers;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A committed change to the platform's settings takes effect on the replica that made it before the write returns, not at
// its next poll (ADR-1219).
public class PlatformSettingsConvergenceTests
{
    #region PatchPlatformSettings Tests

    [Fact]
    public async Task PatchPlatformSettings_WhenItNarrowsADiscoverableList_ServesTheChangeAtOnce()
    {
        using var factory = CreateFactory();
        using var administrator = await CreateAdministratorClientAsync(factory);
        var before = await GetIdTokenSigningAlgorithmsAsync(administrator);
        Assert.Contains("PS256", before);

        await PatchPlatformSettingAsync(
            administrator,
            OpenIdSettingNames.IdTokenSigningAlgValuesSupported,
            ["RS256"]
        );

        // No polling: the change is served as soon as the write returns.
        var after = await GetIdTokenSigningAlgorithmsAsync(administrator);
        Assert.Equal<string>(["RS256"], after);
    }

    #endregion

    #region Scaffolding

    private static BootstrapAdminApplicationFactory CreateFactory() => new();

    private static Task<HttpClient> CreateAdministratorClientAsync(
        HostApplicationFactory factory
    ) =>
        factory.CreateManagementClientAsync(
            BootstrapAdminApplicationFactory.ClientId,
            BootstrapAdminApplicationFactory.ClientSecret
        );

    private static async Task<List<string>> GetIdTokenSigningAlgorithmsAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/.well-known/openid-configuration");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, body);

        var jsonElement = JsonElement.Parse(body);
        return jsonElement
            .GetProperty(OpenIdSettingNames.IdTokenSigningAlgValuesSupported)
            .EnumerateArray()
            .Select(value => value.GetString() ?? string.Empty)
            .ToList();
    }

    private static async Task PatchPlatformSettingAsync(
        HttpClient administrator,
        string settingName,
        string[] values
    )
    {
        var operations = new[]
        {
            new
            {
                op = "add",
                path = $"/{settingName}",
                value = values,
            },
        };
        using var request = new HttpRequestMessage(HttpMethod.Patch, "/api/platform/settings")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(operations),
                SecureEncoding.UTF8,
                "application/json-patch+json"
            ),
        };

        using var response = await administrator.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.NoContent, body);
    }

    #endregion
}
