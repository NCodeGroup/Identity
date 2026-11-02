// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class DevelopmentEnvironmentRegistrationTests
{
    private static IConfiguration CreateConfiguration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(
                values.Select(pair => new KeyValuePair<string, string?>(pair.Key, pair.Value))
            )
            .Build();

    private static ServiceCollection AddDevelopmentEnvironment(IConfiguration configuration)
    {
        var serviceCollection = new ServiceCollection();
        new ServiceBuilder<IdentityServer>(serviceCollection).AddDevelopmentEnvironment(
            configuration
        );
        return serviceCollection;
    }

    #region AddDevelopmentEnvironment Tests

    [Fact]
    public void AddDevelopmentEnvironment_WhenNothingIsConfigured_RegistersNoConvenience()
    {
        var serviceCollection = AddDevelopmentEnvironment(CreateConfiguration());

        Assert.DoesNotContain(
            serviceCollection,
            descriptor => descriptor.ImplementationType == typeof(DeveloperSecretPruningSeedHandler)
        );
        Assert.DoesNotContain(
            serviceCollection,
            descriptor => descriptor.ServiceType == typeof(ICorsService)
        );
    }

    [Fact]
    public void AddDevelopmentEnvironment_WhenPruneUnreadableKeysIsTrue_RegistersThePruner()
    {
        var serviceCollection = AddDevelopmentEnvironment(
            CreateConfiguration(("DeveloperKeys:PruneUnreadableKeys", "true"))
        );

        Assert.Contains(
            serviceCollection,
            descriptor => descriptor.ImplementationType == typeof(DeveloperSecretPruningSeedHandler)
        );
    }

    [Fact]
    public void AddDevelopmentEnvironment_WhenCorsOriginsAreConfigured_RegistersTheCorsPolicy()
    {
        var serviceCollection = AddDevelopmentEnvironment(
            CreateConfiguration(("Cors:AllowedOrigins:0", "https://localhost:5173"))
        );

        Assert.Contains(
            serviceCollection,
            descriptor => descriptor.ServiceType == typeof(ICorsService)
        );
    }

    #endregion
}
