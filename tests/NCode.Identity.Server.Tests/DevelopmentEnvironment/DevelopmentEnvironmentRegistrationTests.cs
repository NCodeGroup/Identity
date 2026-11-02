// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Server.Tests.DevelopmentEnvironment;

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
        var services = new ServiceCollection();
        new ServiceBuilder<IdentityServer>(services).AddDevelopmentEnvironment(configuration);
        return services;
    }

    #region AddDevelopmentEnvironment Tests

    [Fact]
    public void AddDevelopmentEnvironment_WhenNothingIsConfigured_RegistersNoConvenience()
    {
        var services = AddDevelopmentEnvironment(CreateConfiguration());

        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ImplementationType == typeof(DeveloperSecretPruningSeedHandler)
        );
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(ICorsService)
        );
    }

    [Fact]
    public void AddDevelopmentEnvironment_WhenPruneUnreadableKeysIsTrue_RegistersThePruner()
    {
        var services = AddDevelopmentEnvironment(
            CreateConfiguration(("DeveloperKeys:PruneUnreadableKeys", "true"))
        );

        Assert.Contains(
            services,
            descriptor => descriptor.ImplementationType == typeof(DeveloperSecretPruningSeedHandler)
        );
    }

    [Fact]
    public void AddDevelopmentEnvironment_WhenCorsOriginsAreConfigured_RegistersTheCorsPolicy()
    {
        var services = AddDevelopmentEnvironment(
            CreateConfiguration(("Cors:AllowedOrigins:0", "https://localhost:5173"))
        );

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(ICorsService));
    }

    #endregion
}
