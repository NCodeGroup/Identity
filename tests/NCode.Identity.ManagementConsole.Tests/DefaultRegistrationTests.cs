// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.HostFiltering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore;
using NCode.Identity.AspNetCore.Options;
using NCode.Identity.Bff;
using NCode.Identity.Bff.Options;
using NCode.Identity.ManagementConsole.Options;
using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

public class DefaultRegistrationTests : BaseTests
{
    private static IConfiguration CreateConfiguration(
        bool withFrontend = true,
        bool? localDevelopment = null
    )
    {
        var values = new Dictionary<string, string?>
        {
            ["ManagementConsole:ManagementApiUrl"] = "https://id.example.com/api",
            ["ManagementConsole:BundleRootPath"] = "bundle",
        };

        if (localDevelopment is { } enabled)
        {
            values["LocalDevelopment:Enabled"] = enabled ? "true" : "false";
        }

        if (withFrontend)
        {
            values["Bff:Frontends:mgmt:Authority"] = "https://id.example.com";
            values["Bff:Frontends:mgmt:PublicOrigin"] = "https://console.example.com";
            values["Bff:Frontends:mgmt:ClientId"] = "mgmt-console";
            values["Bff:Frontends:mgmt:ReturnUrls:0"] = "/";
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    // The BFF reads the host environment, which a host's builder registers.
    private static ServiceCollection CreateServices(string environmentName = "Production")
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton<IHostEnvironment>(
            new ProductionHostEnvironment { EnvironmentName = environmentName }
        );
        return serviceCollection;
    }

    #region AddManagementConsole Tests

    [Fact]
    public void AddManagementConsole_WithConfiguration_ExposesTheBffBuilder()
    {
        var serviceCollection = CreateServices();

        var builder = serviceCollection.AddManagementConsole(CreateConfiguration());

        Assert.Same(serviceCollection, builder.ServiceCollection);
        Assert.NotNull(builder.IdentityBffBuilder);
        Assert.Same(serviceCollection, builder.IdentityBffBuilder.ServiceCollection);
    }

    [Fact]
    public async Task AddManagementConsole_WithConfiguration_BindsTheConsoleAndBffOptions()
    {
        var serviceCollection = CreateServices();

        serviceCollection.AddManagementConsole(CreateConfiguration());

        await using var provider = serviceCollection.BuildServiceProvider();
        var console = provider.GetRequiredService<IOptions<ManagementConsoleOptions>>().Value;
        var bff = provider.GetRequiredService<IOptions<BffOptions>>().Value;
        Assert.Equal("https://id.example.com/api", console.ManagementApiUrl);
        Assert.Equal("bundle", console.BundleRootPath);
        Assert.Equal(["mgmt"], bff.Frontends.Keys);
        Assert.NotNull(provider.GetRequiredService<IBffTokenAccessor>());
    }

    [Fact]
    public async Task AddManagementConsole_Always_RegistersTheHandlersAndTheBundle()
    {
        var serviceCollection = CreateServices();

        serviceCollection.AddManagementConsole(CreateConfiguration());

        await using var provider = serviceCollection.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<SinglePageBundle<ManagementConsolePage>>());
        Assert.NotNull(provider.GetRequiredService<ManagementConsoleBootstrapHandler>());
        Assert.NotNull(provider.GetRequiredService<ManagementConsolePageHandler>());
    }

    [Fact]
    public void AddManagementConsole_Always_RegistersTheValidators()
    {
        var serviceCollection = CreateServices();

        serviceCollection.AddManagementConsole(CreateConfiguration());

        Assert.Contains(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IValidateOptions<ManagementConsoleOptions>)
                && x.ImplementationType == typeof(ManagementConsoleOptionsValidator)
                && x.Lifetime == ServiceLifetime.Singleton
        );
        Assert.Contains(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IValidateOptions<BffOptions>)
                && x.ImplementationType == typeof(ManagementConsoleFrontendValidator)
                && x.Lifetime == ServiceLifetime.Singleton
        );
        Assert.Contains(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IValidateOptions<HostFilteringOptions>)
                && x.ImplementationType == typeof(ManagementConsoleHostFilteringValidator)
                && x.Lifetime == ServiceLifetime.Singleton
        );
    }

    [Fact]
    public async Task AddManagementConsole_WhenTheFrontendIsMissing_FailsWhenTheBffOptionsAreRead()
    {
        var serviceCollection = CreateServices();
        serviceCollection.AddManagementConsole(CreateConfiguration(withFrontend: false));

        await using var provider = serviceCollection.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IOptions<BffOptions>>();

        var exception = Assert.Throws<OptionsValidationException>(() => accessor.Value);
        Assert.Contains(
            "Bff:Frontends must configure the 'mgmt' frontend that serves the management console.",
            exception.Failures
        );
    }

    [Fact]
    public async Task AddManagementConsole_WithoutTheLocalDevelopmentSection_LeavesTheSwitchOff()
    {
        var serviceCollection = CreateServices("Development");
        serviceCollection.AddManagementConsole(CreateConfiguration());

        await using var provider = serviceCollection.BuildServiceProvider();

        Assert.False(
            provider.GetRequiredService<IOptions<LocalDevelopmentOptions>>().Value.Enabled
        );
    }

    [Fact]
    public async Task AddManagementConsole_WithTheLocalDevelopmentSwitchOn_BindsTheSwitch()
    {
        var serviceCollection = CreateServices("Development");
        serviceCollection.AddManagementConsole(CreateConfiguration(localDevelopment: true));

        await using var provider = serviceCollection.BuildServiceProvider();

        Assert.True(provider.GetRequiredService<IOptions<LocalDevelopmentOptions>>().Value.Enabled);
    }

    [Fact]
    public async Task AddManagementConsole_WithTheLocalDevelopmentSwitchOnInProduction_FailsWhenTheSwitchIsRead()
    {
        var serviceCollection = CreateServices();
        serviceCollection.AddManagementConsole(CreateConfiguration(localDevelopment: true));

        await using var provider = serviceCollection.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IOptions<LocalDevelopmentOptions>>();

        Assert.Throws<OptionsValidationException>(() => accessor.Value);
    }

    [Fact]
    public async Task AddManagementConsole_WithoutAllowedHostsAndTheSwitchOff_FailsWhenTheHostFilteringOptionsAreRead()
    {
        var serviceCollection = CreateServices("Development");
        serviceCollection.AddManagementConsole(CreateConfiguration());

        await using var provider = serviceCollection.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IOptions<HostFilteringOptions>>();

        Assert.Throws<OptionsValidationException>(() => accessor.Value);
    }

    [Fact]
    public async Task AddManagementConsole_WithoutAllowedHostsAndTheSwitchOn_AcceptsTheHostFilteringOptions()
    {
        var serviceCollection = CreateServices("Development");
        serviceCollection.AddManagementConsole(CreateConfiguration(localDevelopment: true));

        await using var provider = serviceCollection.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IOptions<HostFilteringOptions>>().Value);
    }

    [Fact]
    public void AddManagementConsole_WithNullConfiguration_Throws()
    {
        var serviceCollection = CreateServices();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            serviceCollection.AddManagementConsole(null!)
        );

        Assert.Equal("configuration", exception.ParamName);
    }

    #endregion

    private sealed class ProductionHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;

        public string ApplicationName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
