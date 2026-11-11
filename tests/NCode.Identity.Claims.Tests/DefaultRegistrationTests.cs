// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Registration;

namespace NCode.Identity.Claims.Tests;

public class DefaultRegistrationTests
{
    #region Helpers

    private sealed class TestServiceCollection : List<ServiceDescriptor>, IServiceCollection;

    #endregion

    #region AddClaimsLibrary Tests

    [Fact]
    public void AddClaimsLibrary_Always_ReturnsTheMarkerBuilder()
    {
        var serviceCollection = new TestServiceCollection();

        var builder = serviceCollection.AddClaimsLibrary();

        Assert.IsAssignableFrom<IServiceBuilder<ClaimsLibrary>>(builder);
        Assert.Same(serviceCollection, builder.ServiceCollection);
    }

    [Fact]
    public void AddClaimsLibrary_Always_RegistersTheLibraryServices()
    {
        var serviceCollection = new TestServiceCollection();

        serviceCollection.AddClaimsLibrary();

        var service = Assert.Single(
            serviceCollection,
            x => x.ServiceType == typeof(IClaimsService)
        );
        Assert.Equal(ServiceLifetime.Singleton, service.Lifetime);
        Assert.Equal(typeof(DefaultClaimsService), service.ImplementationType);

        var serializer = Assert.Single(
            serviceCollection,
            x => x.ServiceType == typeof(IClaimsSerializer)
        );
        Assert.Equal(ServiceLifetime.Singleton, serializer.Lifetime);
        Assert.Same(DefaultClaimsSerializer.Singleton, serializer.ImplementationInstance);
    }

    [Fact]
    public void AddClaimsLibrary_WhenCalledTwice_Throws()
    {
        var serviceCollection = new TestServiceCollection();
        serviceCollection.AddClaimsLibrary();

        Assert.Throws<InvalidOperationException>(() => serviceCollection.AddClaimsLibrary());

        Assert.Single(serviceCollection, x => x.ServiceType == typeof(IClaimsService));
        Assert.Single(serviceCollection, x => x.ServiceType == typeof(IClaimsSerializer));
    }

    #endregion
}
