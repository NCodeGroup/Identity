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
