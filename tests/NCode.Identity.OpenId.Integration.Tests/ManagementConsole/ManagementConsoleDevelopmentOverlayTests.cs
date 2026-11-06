// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.ManagementConsole;
using NCode.Identity.OpenId.Integration.Tests.Infrastructure;
using NCode.Identity.Server.DevelopmentEnvironment;
using Xunit;

namespace NCode.Identity.OpenId.Integration.Tests.ManagementConsole;

/// <summary>
/// Guards the Development configuration of the management console's host and of the reference OpenID provider's
/// developer seed: the client the console logs in as and the URIs the provider registers for it are declared in two
/// hosts, and must not drift apart.
/// </summary>
public class ManagementConsoleDevelopmentOverlayTests(
    DeveloperSeedingHostApplicationFactory providerFactory
) : IClassFixture<DeveloperSeedingHostApplicationFactory>
{
    private static BffFrontendOptions GetConsoleFrontend()
    {
        using var consoleFactory = new ManagementConsoleApplicationFactory();
        return consoleFactory.Services.GetRequiredService<IOptions<BffOptions>>().Value.Frontends[
            ManagementConsoleDefaults.FrontendName
        ];
    }

    private DeveloperSeedingHostApplicationFactory ProviderFactory { get; } = providerFactory;

    private DeveloperBffClientSeed GetSeededConsoleClient() =>
        Assert.Single(
            ProviderFactory
                .Services.GetRequiredService<DeveloperInteractiveSeedOptions>()
                .BffClients
        );

    #region BffClients Tests

    [Fact]
    public void BffClients_InDevelopment_RegistersTheClientTheConsoleLogsInAs()
    {
        var seed = GetSeededConsoleClient();
        var frontend = GetConsoleFrontend();

        Assert.False(string.IsNullOrEmpty(frontend.ClientId));
        Assert.Equal(frontend.ClientId, seed.ClientId);
    }

    [Fact]
    public void BffClients_InDevelopment_RegistersTheConsolesCallbackUri()
    {
        var seed = GetSeededConsoleClient();
        var frontend = GetConsoleFrontend();

        Assert.Contains(
            frontend.PublicOrigin + frontend.PathPrefix + "/callback",
            seed.RedirectUris
        );
    }

    [Fact]
    public void BffClients_InDevelopment_RegistersTheConsolesPostLogoutUri()
    {
        var seed = GetSeededConsoleClient();
        var frontend = GetConsoleFrontend();

        Assert.Contains(frontend.PublicOrigin + "/", seed.PostLogoutRedirectUris);
    }

    [Fact]
    public void BffClients_InDevelopment_RegistersTheConsolesBackChannelLogoutUri()
    {
        var seed = GetSeededConsoleClient();
        var frontend = GetConsoleFrontend();

        Assert.Equal(
            frontend.PublicOrigin + frontend.PathPrefix + "/backchannel-logout",
            seed.BackchannelLogoutUri
        );
    }

    #endregion
}
