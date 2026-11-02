// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.Endpoints;

// The server records the effective settings it reconciled against in the form the setting serializer reads back, so
// the next reconciliation can compare a change with them. See ADR-1234.
public class ReconciledServerSettingsTests
{
    #region Reconciliation Tests

    [Fact]
    public async Task Reconciliation_RecordsTheEffectiveSettingsInTheFormTheSettingSerializerReads()
    {
        using var factory = new HostApplicationFactory();
        using var client = factory.CreateClient();

        // The first request materializes the server, which starts its reconciliation.
        using var response = await client.GetAsync("/oauth2/jwks");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var reconciledSettings = await WaitForReconciledSettingsAsync(factory);

        Assert.NotNull(reconciledSettings);
        using var serviceScope = factory.Services.CreateScope();
        var jsonOptions = serviceScope
            .ServiceProvider.GetRequiredService<IOpenIdEnvironmentProvider>()
            .Get()
            .JsonSerializerOptions;
        var settings = serviceScope
            .ServiceProvider.GetRequiredService<ISettingSerializer>()
            .DeserializeSettings(reconciledSettings.Value, OpenIdSettingScopes.Server, jsonOptions);
        Assert.Equal(reconciledSettings.Value.EnumerateObject().Count(), settings.Count);
        Assert.Contains(
            settings,
            setting =>
                setting.Descriptor.Name == OpenIdSettingNames.IdTokenSigningAlgValuesSupported
        );
    }

    #endregion

    #region Scaffolding

    // The reconciliation of the server's settings runs in the background, so the record arrives after the first request
    // returns.
    private static async Task<JsonElement?> WaitForReconciledSettingsAsync(
        HostApplicationFactory factory
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var server = await GetServerAsync(factory);
        while (server?.ReconciledSettings is null && stopwatch.Elapsed < TimeSpan.FromSeconds(15))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100));
            server = await GetServerAsync(factory);
        }

        return server?.ReconciledSettings;
    }

    private static async Task<PersistedServer?> GetServerAsync(HostApplicationFactory factory)
    {
        using var serviceScope = factory.Services.CreateScope();
        await using var storeManager = await serviceScope
            .ServiceProvider.GetRequiredService<IStoreManagerFactory>()
            .CreateAsync(CancellationToken.None);
        return await storeManager
            .GetStore<IServerStore>()
            .GetOrDefaultAsync(OpenIdServerOptions.DefaultServerId, CancellationToken.None);
    }

    #endregion
}
