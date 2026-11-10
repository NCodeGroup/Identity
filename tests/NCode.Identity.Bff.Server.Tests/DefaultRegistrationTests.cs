// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.Bff.Options;
using NCode.Identity.OpenId.Client;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.Bff.Server.Tests;

public class DefaultRegistrationTests : BaseTests
{
    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Bff:Frontends:web:Authority"] = "https://op.example",
                    ["Bff:Frontends:web:PublicOrigin"] = "https://orders.example",
                    ["Bff:Frontends:web:ClientId"] = "orders-web",
                    ["Bff:Frontends:web:ReturnUrls:0"] = "/",
                }
            )
            .Build();

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
    public void AddIdentityBff_Always_RegistersTheRefreshLeaseValidator()
    {
        var serviceCollection = CreateServices();

        serviceCollection.AddIdentityBff(CreateConfiguration());

        Assert.Contains(
            serviceCollection,
            x =>
                x.ServiceType == typeof(IValidateOptions<BffOptions>)
                && x.ImplementationType == typeof(BffRefreshLeaseValidator)
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
}
