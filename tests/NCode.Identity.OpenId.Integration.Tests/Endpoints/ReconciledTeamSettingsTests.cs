// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers.Text;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Buffers;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// A team records the effective settings it reconciled against in the form the setting serializer reads back, so the
// next reconciliation can compare a change with them. A change to the platform's settings reconciles every team. See
// ADR-1234 and ADR-1403.
public class ReconciledTeamSettingsTests
{
    private const string BootstrapClientId = "it-bootstrap-admin";
    private const string BootstrapClientSecret = "bootstrap-secret-value";
    private const string TeamId = "platform";

    #region Reconciliation Tests

    [Fact]
    public async Task Reconciliation_WhenThePlatformSettingsChange_RecordsTheTeamsEffectiveSettings()
    {
        using var factory = CreateFactory();
        using var administrator = await factory.CreateManagementClientAsync(
            BootstrapClientId,
            BootstrapClientSecret
        );

        await PatchPlatformSettingAsync(
            administrator,
            OpenIdSettingNames.IdTokenSigningAlgValuesSupported,
            ["RS256"]
        );

        var team = await WaitForReconciledTeamAsync(factory);

        Assert.NotNull(team);
        Assert.NotNull(team.ReconciledSettingsFingerprint);
        Assert.NotNull(team.ReconciledSettings);
        var reconciledSettings = team.ReconciledSettings.Value;
        using var serviceScope = factory.Services.CreateScope();
        var jsonOptions = serviceScope
            .ServiceProvider.GetRequiredService<IOpenIdEnvironmentProvider>()
            .Get()
            .JsonSerializerOptions;
        var settings = serviceScope
            .ServiceProvider.GetRequiredService<ISettingSerializer>()
            .DeserializeSettings(reconciledSettings, OpenIdSettingScopes.Team, jsonOptions);
        Assert.Equal(reconciledSettings.EnumerateObject().Count(), settings.Count);
        Assert.Contains(
            settings,
            setting =>
                setting.Descriptor.Name == OpenIdSettingNames.IdTokenSigningAlgValuesSupported
        );
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

    // The reconciliation of a team's settings runs in the background, so the record arrives after the write returns.
    private static async Task<PersistedTeam?> WaitForReconciledTeamAsync(
        HostApplicationFactory factory
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var team = await GetTeamAsync(factory);
        while (
            !RecordsTheChange(team?.ReconciledSettings)
            && stopwatch.Elapsed < TimeSpan.FromSeconds(15)
        )
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            team = await GetTeamAsync(factory);
        }

        return team;
    }

    private static bool RecordsTheChange(JsonElement? reconciledSettings) =>
        reconciledSettings is { ValueKind: JsonValueKind.Object } json
        && json.TryGetProperty(
            OpenIdSettingNames.IdTokenSigningAlgValuesSupported,
            out var algorithms
        )
        && algorithms.GetArrayLength() == 1;

    private static async Task<PersistedTeam?> GetTeamAsync(HostApplicationFactory factory)
    {
        using var serviceScope = factory.Services.CreateScope();
        await using var storeManager = await serviceScope
            .ServiceProvider.GetRequiredService<IStoreManagerFactory>()
            .CreateAsync(CancellationToken.None);
        return await storeManager
            .GetStore<ITeamStore>()
            .GetOrDefaultAsync(TeamId, CancellationToken.None);
    }

    #endregion
}
