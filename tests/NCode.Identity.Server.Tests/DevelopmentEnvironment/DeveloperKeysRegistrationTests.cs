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

using Microsoft.Extensions.DependencyInjection;
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
        Assert.True(IsProvisioningEnabled(services));
    }

    #endregion
}
