// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Server.DevelopmentEnvironment;
using NCode.Mediator;
using NCode.Registration;
using Xunit;

namespace NCode.Identity.Server.Tests;

public sealed class DeveloperKeysRegistrationTests
{
    private static (
        IServiceCollection ServiceCollection,
        IServiceBuilder<IdentityServer> Builder
    ) CreateBuilder()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddOptions<OpenIdOptions>();
        return (serviceCollection, new ServiceBuilder<IdentityServer>(serviceCollection));
    }

    private static bool IsProvisioningEnabled(IServiceCollection serviceCollection)
    {
        using var serviceProvider = serviceCollection.BuildServiceProvider();
        return serviceProvider
            .GetRequiredService<IOptions<OpenIdOptions>>()
            .Value.Tenant.KeyProvisioning.Enabled;
    }

    #region AddDeveloperKeyPruning Tests

    [Fact]
    public void AddDeveloperKeyPruning_PrunesUnreadableKeysAndKeepsProvisioning()
    {
        var (serviceCollection, builder) = CreateBuilder();

        builder.AddDeveloperKeyPruning();

        var handler = Assert.Single(
            serviceCollection,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<SeedTenantCommand>)
        );
        Assert.Equal(typeof(DeveloperSecretPruningSeedHandler), handler.ImplementationType);
        Assert.Single(
            serviceCollection,
            descriptor => descriptor.ServiceType == typeof(DeveloperMasterKeyReset)
        );
        Assert.True(IsProvisioningEnabled(serviceCollection));
    }

    [Fact]
    public void AddDeveloperKeyPruning_WhenCalledTwice_RegistersThePlatformHealOnce()
    {
        var (serviceCollection, builder) = CreateBuilder();

        builder.AddDeveloperKeyPruning();
        builder.AddDeveloperKeyPruning();

        var hostedService = Assert.Single(
            serviceCollection,
            x => x.ServiceType == typeof(IHostedService)
        );
        Assert.Equal(typeof(DeveloperPlatformMasterKeyPruning), hostedService.ImplementationType);
    }

    #endregion
}
