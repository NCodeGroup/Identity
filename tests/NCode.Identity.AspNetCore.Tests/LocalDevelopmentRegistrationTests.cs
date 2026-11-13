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

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        return services;
    }

    #region AddLocalDevelopmentOptions Tests

    [Fact]
    public void AddLocalDevelopmentOptions_WhenEnabledIsTrue_BindsTheValue()
    {
        var services = CreateServices();
        services.AddLocalDevelopmentOptions(
            CreateConfiguration(
                new Dictionary<string, string?> { ["LocalDevelopment:Enabled"] = "true" }
            )
        );

        using var provider = services.BuildServiceProvider();

        Assert.True(provider.GetRequiredService<IOptions<LocalDevelopmentOptions>>().Value.Enabled);
    }

    [Fact]
    public void AddLocalDevelopmentOptions_WhenSectionIsAbsent_BindsFalse()
    {
        var services = CreateServices();
        services.AddLocalDevelopmentOptions(CreateConfiguration([]));

        using var provider = services.BuildServiceProvider();

        Assert.False(
            provider.GetRequiredService<IOptions<LocalDevelopmentOptions>>().Value.Enabled
        );
    }

    [Fact]
    public void AddLocalDevelopmentOptions_WithAnUnknownKey_ThrowsWhenTheOptionsAreRead()
    {
        var services = CreateServices();
        services.AddLocalDevelopmentOptions(
            CreateConfiguration(
                new Dictionary<string, string?> { ["LocalDevelopment:Enabeld"] = "true" }
            )
        );

        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
            provider.GetRequiredService<IOptions<LocalDevelopmentOptions>>().Value
        );
    }

    [Fact]
    public void AddLocalDevelopmentOptions_CalledTwice_RegistersOneValidator()
    {
        var services = new ServiceCollection();
        var configuration = CreateConfiguration([]);

        services.AddLocalDevelopmentOptions(configuration);
        var countAfterFirstCall = services.Count;
        services.AddLocalDevelopmentOptions(configuration);

        Assert.Equal(countAfterFirstCall, services.Count);
        Assert.Single(
            services,
            descriptor =>
                descriptor.ServiceType == typeof(IValidateOptions<LocalDevelopmentOptions>)
                && descriptor.ImplementationType == typeof(LocalDevelopmentOptionsValidator)
        );
    }

    [Fact]
    public void AddLocalDevelopmentOptions_ReturnsTheSameServiceCollection()
    {
        var services = new ServiceCollection();

        var result = services.AddLocalDevelopmentOptions(CreateConfiguration([]));

        Assert.Same(services, result);
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
