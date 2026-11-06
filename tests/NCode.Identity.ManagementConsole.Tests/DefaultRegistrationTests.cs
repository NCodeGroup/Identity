// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff;
using NCode.Identity.Bff.Options;
using Xunit;

namespace NCode.Identity.ManagementConsole.Tests;

public class DefaultRegistrationTests : BaseTests
{
    private static IConfiguration CreateConfiguration(bool withFrontend = true)
    {
        var values = new Dictionary<string, string?>
        {
            ["ManagementConsole:ManagementApiUrl"] = "https://id.example.com/api",
            ["ManagementConsole:BundleRootPath"] = "bundle",
        };

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
    private static ServiceCollection CreateServices()
    {
        var mockEnvironment = CreateLooseMock<IHostEnvironment>();
        mockEnvironment.Setup(x => x.ContentRootPath).Returns(AppContext.BaseDirectory);
        mockEnvironment.Setup(x => x.EnvironmentName).Returns("Production");

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(mockEnvironment.Object);
        return services;
    }

    #region AddManagementConsole Tests

    [Fact]
    public void AddManagementConsole_WithConfiguration_ExposesTheBffBuilder()
    {
        var services = CreateServices();

        var builder = services.AddManagementConsole(CreateConfiguration());

        Assert.Same(services, builder.ServiceCollection);
        Assert.NotNull(builder.IdentityBffBuilder);
        Assert.Same(services, builder.IdentityBffBuilder.ServiceCollection);
    }

    [Fact]
    public async Task AddManagementConsole_WithConfiguration_BindsTheConsoleAndBffOptions()
    {
        var services = CreateServices();

        services.AddManagementConsole(CreateConfiguration());

        await using var provider = services.BuildServiceProvider();
        var console = provider.GetRequiredService<IOptions<ManagementConsoleOptions>>().Value;
        var bff = provider.GetRequiredService<IOptions<BffOptions>>().Value;
        Assert.Equal("https://id.example.com/api", console.ManagementApiUrl);
        Assert.Equal("bundle", console.BundleRootPath);
        Assert.Equal(["mgmt"], bff.Frontends.Keys);
        Assert.NotNull(provider.GetRequiredService<IBffTokenAccessor>());
    }

    [Fact]
    public async Task AddManagementConsole_Always_RegistersTheHandlersAndTheReader()
    {
        var services = CreateServices();

        services.AddManagementConsole(CreateConfiguration());

        await using var provider = services.BuildServiceProvider();
        Assert.NotNull(provider.GetRequiredService<ManagementConsoleBundleReader>());
        Assert.NotNull(provider.GetRequiredService<ManagementConsoleBootstrapHandler>());
        Assert.NotNull(provider.GetRequiredService<ManagementConsolePageHandler>());
        Assert.NotNull(provider.GetRequiredService<ManagementConsoleAssetHandler>());
    }

    [Fact]
    public void AddManagementConsole_Always_RegistersBothValidators()
    {
        var services = CreateServices();

        services.AddManagementConsole(CreateConfiguration());

        Assert.Contains(
            services,
            x =>
                x.ServiceType == typeof(IValidateOptions<ManagementConsoleOptions>)
                && x.ImplementationType == typeof(ManagementConsoleOptionsValidator)
                && x.Lifetime == ServiceLifetime.Singleton
        );
        Assert.Contains(
            services,
            x =>
                x.ServiceType == typeof(IValidateOptions<BffOptions>)
                && x.ImplementationType == typeof(ManagementConsoleFrontendValidator)
                && x.Lifetime == ServiceLifetime.Singleton
        );
    }

    [Fact]
    public async Task AddManagementConsole_WhenTheFrontendIsMissing_FailsWhenTheBffOptionsAreRead()
    {
        var services = CreateServices();
        services.AddManagementConsole(CreateConfiguration(withFrontend: false));

        await using var provider = services.BuildServiceProvider();
        var accessor = provider.GetRequiredService<IOptions<BffOptions>>();

        var exception = Assert.Throws<OptionsValidationException>(() => accessor.Value);
        Assert.Contains(
            "Bff:Frontends must configure the 'mgmt' frontend that serves the management console.",
            exception.Failures
        );
    }

    [Fact]
    public void AddManagementConsole_WithNullConfiguration_Throws()
    {
        var services = CreateServices();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            services.AddManagementConsole(null!)
        );

        Assert.Equal("configuration", exception.ParamName);
    }

    #endregion

    #region ManagementConsoleFrontendValidator Tests

    [Fact]
    public void Validate_WhenTheMgmtFrontendIsConfigured_Succeeds()
    {
        var options = new BffOptions
        {
            Frontends = new Dictionary<string, BffFrontendOptions>
            {
                ["mgmt"] = new BffFrontendOptions(),
            },
        };

        var result = new ManagementConsoleFrontendValidator().Validate(null, options);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_WhenTheMgmtFrontendIsMissing_Fails()
    {
        var options = new BffOptions
        {
            Frontends = new Dictionary<string, BffFrontendOptions>
            {
                ["web"] = new BffFrontendOptions(),
            },
        };

        var result = new ManagementConsoleFrontendValidator().Validate(null, options);

        Assert.True(result.Failed);
        Assert.Equal(
            "Bff:Frontends must configure the 'mgmt' frontend that serves the management console.",
            Assert.Single(result.Failures)
        );
    }

    #endregion
}
