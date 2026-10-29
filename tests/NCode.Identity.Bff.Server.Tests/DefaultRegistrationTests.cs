#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
                    ["Bff:Authority"] = "https://op.example",
                    ["Bff:ClientId"] = "orders-web",
                    ["Bff:ReturnUrls:0"] = "/",
                }
            )
            .Build();

    // The BFF reads the host environment, which a host's builder registers.
    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(CreateLooseMock<IHostEnvironment>().Object);
        return services;
    }

    #region AddIdentityBff Tests

    [Fact]
    public async Task AddIdentityBff_WithConfiguration_ComposesTheBffAndItsLibraries()
    {
        var services = CreateServices();

        services.AddIdentityBff(CreateConfiguration());

        await using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<BffOptions>>().Value;
        Assert.Equal("https://op.example", options.Authority);
        Assert.Equal("orders-web", options.ClientId);
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
        var services = CreateServices();

        var builder = services.AddIdentityBff(CreateConfiguration());

        Assert.Same(services, builder.ServiceCollection);
        Assert.Same(services, builder.SharedStateLibraryBuilder.ServiceCollection);
        Assert.Same(services, builder.EventsLibraryBuilder.ServiceCollection);
        Assert.Same(services, builder.SecretsLibraryBuilder.ServiceCollection);
        Assert.Same(services, builder.JoseLibraryBuilder.ServiceCollection);
        Assert.Same(services, builder.JwtLibraryBuilder.ServiceCollection);
        Assert.Same(services, builder.OpenIdClientLibraryBuilder.ServiceCollection);
        Assert.Same(services, builder.BffLibraryBuilder.ServiceCollection);
    }

    [Fact]
    public async Task AddIdentityBff_WhenTheHostRegisteredItsOwnSharedState_KeepsIt()
    {
        // A deployment that runs several replicas registers shared adapters; the in-process defaults never replace them.
        var services = CreateServices();
        var valueSet = CreateLooseMock<IExpiringValueSet>().Object;
        services.AddSingleton(valueSet);

        services.AddIdentityBff(CreateConfiguration());

        await using var provider = services.BuildServiceProvider();
        Assert.Same(valueSet, provider.GetRequiredService<IExpiringValueSet>());
    }

    [Fact]
    public void AddIdentityBff_Always_RegistersTheRefreshLeaseValidator()
    {
        var services = CreateServices();

        services.AddIdentityBff(CreateConfiguration());

        Assert.Contains(
            services,
            x =>
                x.ServiceType == typeof(IValidateOptions<BffOptions>)
                && x.ImplementationType == typeof(BffRefreshLeaseValidator)
                && x.Lifetime == ServiceLifetime.Singleton
        );
    }

    [Fact]
    public void AddIdentityBff_WithNullConfiguration_Throws()
    {
        var services = CreateServices();

        var exception = Assert.Throws<ArgumentNullException>(() => services.AddIdentityBff(null!));

        Assert.Equal("configuration", exception.ParamName);
    }

    #endregion
}
