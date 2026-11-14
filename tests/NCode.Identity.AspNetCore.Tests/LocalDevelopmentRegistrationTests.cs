// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore.Options;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests;

public sealed class LocalDevelopmentRegistrationTests
{
    private static IConfiguration CreateConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();

    private static ServiceCollection CreateServiceCollection()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        return serviceCollection;
    }

    #region AddLocalDevelopmentOptions Tests

    [Fact]
    public void AddLocalDevelopmentOptions_WhenEnabledIsTrue_BindsTheValue()
    {
        var serviceCollection = CreateServiceCollection();
        serviceCollection.AddLocalDevelopmentOptions(
            CreateConfiguration(
                new Dictionary<string, string?> { ["LocalDevelopment:Enabled"] = "true" }
            )
        );

        using var provider = serviceCollection.BuildServiceProvider();

        Assert.True(provider.GetRequiredService<IOptions<LocalDevelopmentOptions>>().Value.Enabled);
    }

    [Fact]
    public void AddLocalDevelopmentOptions_WhenSectionIsAbsent_BindsFalse()
    {
        var serviceCollection = CreateServiceCollection();
        serviceCollection.AddLocalDevelopmentOptions(CreateConfiguration([]));

        using var provider = serviceCollection.BuildServiceProvider();

        Assert.False(
            provider.GetRequiredService<IOptions<LocalDevelopmentOptions>>().Value.Enabled
        );
    }

    [Fact]
    public void AddLocalDevelopmentOptions_WhenEnabledIsTrue_RegistersAnEnabledMode()
    {
        var serviceCollection = CreateServiceCollection();
        serviceCollection.AddLocalDevelopmentOptions(
            CreateConfiguration(
                new Dictionary<string, string?> { ["LocalDevelopment:Enabled"] = "true" }
            )
        );

        using var provider = serviceCollection.BuildServiceProvider();

        Assert.True(provider.GetRequiredService<LocalDevelopmentMode>().Enabled);
    }

    [Fact]
    public void AddLocalDevelopmentOptions_WhenSectionIsAbsent_RegistersADisabledMode()
    {
        var serviceCollection = CreateServiceCollection();
        serviceCollection.AddLocalDevelopmentOptions(CreateConfiguration([]));

        using var provider = serviceCollection.BuildServiceProvider();

        Assert.False(provider.GetRequiredService<LocalDevelopmentMode>().Enabled);
    }

    [Fact]
    public void AddLocalDevelopmentOptions_AfterADisabledModeIsRegistered_ReplacesIt()
    {
        var serviceCollection = CreateServiceCollection();
        serviceCollection.AddSingleton(new LocalDevelopmentMode(false));

        serviceCollection.AddLocalDevelopmentOptions(
            CreateConfiguration(
                new Dictionary<string, string?> { ["LocalDevelopment:Enabled"] = "true" }
            )
        );

        Assert.Single(
            serviceCollection,
            descriptor => descriptor.ServiceType == typeof(LocalDevelopmentMode)
        );
        using var provider = serviceCollection.BuildServiceProvider();
        Assert.True(provider.GetRequiredService<LocalDevelopmentMode>().Enabled);
    }

    [Fact]
    public void AddLocalDevelopmentOptions_WithAnUnknownKey_Throws()
    {
        var serviceCollection = CreateServiceCollection();

        Assert.Throws<InvalidOperationException>(() =>
            serviceCollection.AddLocalDevelopmentOptions(
                CreateConfiguration(
                    new Dictionary<string, string?> { ["LocalDevelopment:Enabeld"] = "true" }
                )
            )
        );
    }

    [Fact]
    public void AddLocalDevelopmentOptions_CalledTwice_RegistersOneValidator()
    {
        var serviceCollection = new ServiceCollection();
        var configuration = CreateConfiguration([]);

        serviceCollection.AddLocalDevelopmentOptions(configuration);
        var countAfterFirstCall = serviceCollection.Count;
        serviceCollection.AddLocalDevelopmentOptions(configuration);

        Assert.Equal(countAfterFirstCall, serviceCollection.Count);
        Assert.Single(
            serviceCollection,
            descriptor =>
                descriptor.ServiceType == typeof(IValidateOptions<LocalDevelopmentOptions>)
                && descriptor.ImplementationType == typeof(LocalDevelopmentOptionsValidator)
        );
        Assert.Single(
            serviceCollection,
            descriptor => descriptor.ServiceType == typeof(LocalDevelopmentMode)
        );
    }

    [Fact]
    public void AddLocalDevelopmentOptions_ReturnsTheSameServiceCollection()
    {
        var serviceCollection = new ServiceCollection();

        var result = serviceCollection.AddLocalDevelopmentOptions(CreateConfiguration([]));

        Assert.Same(serviceCollection, result);
    }

    #endregion

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
