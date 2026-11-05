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
        IServiceCollection Services,
        IServiceBuilder<IdentityServer> Builder
    ) CreateBuilder()
    {
        var services = new ServiceCollection();
        services.AddOptions<OpenIdOptions>();
        return (services, new ServiceBuilder<IdentityServer>(services));
    }

    private static bool IsProvisioningEnabled(IServiceCollection services)
    {
        using var serviceProvider = services.BuildServiceProvider();
        return serviceProvider
            .GetRequiredService<IOptions<OpenIdOptions>>()
            .Value.Tenant.KeyProvisioning.Enabled;
    }

    #region AddDeveloperKeyPruning Tests

    [Fact]
    public void AddDeveloperKeyPruning_PrunesUnreadableKeysAndKeepsProvisioning()
    {
        var (services, builder) = CreateBuilder();

        builder.AddDeveloperKeyPruning();

        var handler = Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(ICommandHandler<SeedTenantCommand>)
        );
        Assert.Equal(typeof(DeveloperSecretPruningSeedHandler), handler.ImplementationType);
        Assert.Single(
            services,
            descriptor => descriptor.ServiceType == typeof(DeveloperMasterKeyReset)
        );
        Assert.True(IsProvisioningEnabled(services));
    }

    [Fact]
    public void AddDeveloperKeyPruning_HealsThePlatformWhenTheHostStarts()
    {
        var (services, builder) = CreateBuilder();

        builder.AddDeveloperKeyPruning();
        builder.AddDeveloperKeyPruning();

        var hostedService = Assert.Single(services, x => x.ServiceType == typeof(IHostedService));
        Assert.Equal(typeof(DeveloperPlatformMasterKeyPruning), hostedService.ImplementationType);
    }

    #endregion
}
