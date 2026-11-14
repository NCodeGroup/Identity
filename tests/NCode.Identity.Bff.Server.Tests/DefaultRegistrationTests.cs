// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.Bff.Server.Tests;

public class DefaultRegistrationTests : BaseTests
{
    private static IConfiguration CreateConfiguration(
        string authority = "https://op.example",
        string publicOrigin = "https://orders.example",
        bool? localDevelopment = null
    )
    {
        var values = new Dictionary<string, string?>
        {
            ["Bff:Frontends:web:Authority"] = authority,
            ["Bff:Frontends:web:PublicOrigin"] = publicOrigin,
            ["Bff:Frontends:web:ClientId"] = "orders-web",
            ["Bff:Frontends:web:ReturnUrls:0"] = "/",
        };
        if (localDevelopment is { } enabled)
            values["LocalDevelopment:Enabled"] = enabled ? "true" : "false";

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    // The BFF reads the host environment, which a host's builder registers.
    private static ServiceCollection CreateServices()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddLogging();
        serviceCollection.AddSingleton(CreateLooseMock<IHostEnvironment>().Object);
        return serviceCollection;
    }

    #region AddIdentityBff Tests

    [Fact]
    public async Task AddIdentityBff_WithConfiguration_ComposesTheBffAndItsLibraries()
    {
        var serviceCollection = CreateServices();

        serviceCollection.AddIdentityBff(CreateConfiguration());

        await using var provider = serviceCollection.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BffOptions>>().Value;
        Assert.Equal(["web"], options.Frontends.Keys);
        Assert.Equal("https://op.example", options.Frontends["web"].Authority);
        Assert.Equal("orders-web", options.Frontends["web"].ClientId);
        Assert.NotNull(provider.GetRequiredService<IBffTokenAccessor>());
        Assert.NotNull(provider.GetRequiredService<IBffSessionStore>());
        Assert.NotNull(provider.GetRequiredService<ITokenClient>());
        Assert.NotNull(provider.GetRequiredService<IExpiringKeySet>());
        Assert.NotNull(provider.GetRequiredService<IExpiringValueSet>());
        Assert.NotNull(provider.GetRequiredService<IExpiringIndexSet>());
    }

    [Fact]
    public async Task AddIdentityBff_WithHttpUrlsAndTheSwitchOn_ValidatesTheOptions()
    {
        var serviceCollection = CreateServices();
        serviceCollection.AddIdentityBff(
            CreateConfiguration(
                authority: "http://localhost:5000",
                publicOrigin: "http://localhost:5001",
                localDevelopment: true
            )
        );

        await using var provider = serviceCollection.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<BffOptions>>().Value;
        Assert.Equal("http://localhost:5000", options.Frontends["web"].Authority);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(false)]
    public async Task AddIdentityBff_WithHttpUrlsAndTheSwitchOff_FailsValidation(
        bool? localDevelopment
    )
    {
        var serviceCollection = CreateServices();
        serviceCollection.AddIdentityBff(
            CreateConfiguration(
                authority: "http://localhost:5000",
                publicOrigin: "http://localhost:5001",
                localDevelopment: localDevelopment
            )
        );
        await using var provider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<BffOptions>>().Value
        );

        Assert.Contains(exception.Failures, x => x.Contains("Bff:Frontends:web:Authority"));
    }

    [Fact]
    public async Task AddIdentityBff_WithHttpUrlsInDevelopmentWithoutTheSwitch_FailsValidation()
    {
        var serviceCollection = CreateServices();
        serviceCollection.AddSingleton<IHostEnvironment>(
            new TestHostEnvironment { EnvironmentName = Environments.Development }
        );
        serviceCollection.AddIdentityBff(
            CreateConfiguration(
                authority: "http://localhost:5000",
                publicOrigin: "http://localhost:5001"
            )
        );
        await using var provider = serviceCollection.BuildServiceProvider();

        var exception = Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<BffOptions>>().Value
        );

        Assert.Contains(exception.Failures, x => x.Contains("Bff:Frontends:web:Authority"));
    }

    [Fact]
    public void AddIdentityBff_Always_ExposesEveryLibraryBuilderOverTheSameServices()
    {
        var serviceCollection = CreateServices();

        var builder = serviceCollection.AddIdentityBff(CreateConfiguration());

        Assert.Same(serviceCollection, builder.ServiceCollection);
        Assert.Same(serviceCollection, builder.SharedStateLibraryBuilder.ServiceCollection);
        Assert.Same(serviceCollection, builder.EventsLibraryBuilder.ServiceCollection);
        Assert.Same(serviceCollection, builder.SecretsLibraryBuilder.ServiceCollection);
        Assert.Same(serviceCollection, builder.JoseLibraryBuilder.ServiceCollection);
        Assert.Same(serviceCollection, builder.JwtLibraryBuilder.ServiceCollection);
        Assert.Same(serviceCollection, builder.OpenIdClientLibraryBuilder.ServiceCollection);
        Assert.Same(serviceCollection, builder.BffLibraryBuilder.ServiceCollection);
    }

    [Fact]
    public async Task AddIdentityBff_WhenTheHostRegisteredItsOwnSharedState_KeepsIt()
    {
        // A deployment that runs several replicas registers shared adapters; the in-process defaults never replace them.
        var serviceCollection = CreateServices();
        var valueSet = CreateLooseMock<IExpiringValueSet>().Object;
        serviceCollection.AddSingleton(valueSet);

        serviceCollection.AddIdentityBff(CreateConfiguration());

        await using var provider = serviceCollection.BuildServiceProvider();
        Assert.Same(valueSet, provider.GetRequiredService<IExpiringValueSet>());
    }

    [Fact]
    public void AddIdentityBff_Always_RegistersTheRefreshLeaseStartupCheck()
    {
        var serviceCollection = CreateServices();

        serviceCollection.AddIdentityBff(CreateConfiguration());

        Assert.Contains(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IHostedService)
                && x.ImplementationType == typeof(BffRefreshLeaseStartupCheck)
                && x.Lifetime == ServiceLifetime.Singleton
        );
    }

    [Fact]
    public void AddIdentityBff_WithNullConfiguration_Throws()
    {
        var serviceCollection = CreateServices();

        var exception = Assert.Throws<ArgumentNullException>(() =>
            serviceCollection.AddIdentityBff(null!)
        );

        Assert.Equal("configuration", exception.ParamName);
    }

    #endregion

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = string.Empty;

        public string ApplicationName { get; set; } = string.Empty;

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
