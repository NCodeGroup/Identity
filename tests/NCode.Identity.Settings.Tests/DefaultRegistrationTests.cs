// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Registration;

namespace NCode.Identity.Settings.Tests;

public class DefaultRegistrationTests
{
    #region Helpers

    private static void AssertSingleton<TService, TImplementation>(
        IServiceCollection serviceCollection
    )
    {
        var descriptor = Assert.Single(serviceCollection, x => x.ServiceType == typeof(TService));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(TImplementation), descriptor.ImplementationType);
    }

    #endregion

    #region AddSettingsLibrary Tests

    [Fact]
    public void AddSettingsLibrary_Always_ReturnsTheMarkerBuilder()
    {
        var serviceCollection = new ServiceCollection();

        var builder = serviceCollection.AddSettingsLibrary();

        Assert.IsAssignableFrom<IServiceBuilder<SettingsLibrary>>(builder);
        Assert.Same(serviceCollection, builder.ServiceCollection);
    }

    [Fact]
    public void AddSettingsLibrary_Always_RegistersTheLibraryServices()
    {
        var serviceCollection = new ServiceCollection();

        serviceCollection.AddSettingsLibrary();

        AssertSingleton<
            ISettingDescriptorCollectionProvider,
            DefaultSettingDescriptorCollectionProvider
        >(serviceCollection);
        AssertSingleton<ISettingScopeCollectionProvider, DefaultSettingScopeCollectionProvider>(
            serviceCollection
        );
        AssertSingleton<
            IReadOnlySettingCollectionProviderFactory,
            DefaultReadOnlySettingCollectionProviderFactory
        >(serviceCollection);
        AssertSingleton<ISettingCollectionFactory, DefaultSettingCollectionFactory>(
            serviceCollection
        );
        AssertSingleton<ISettingSerializer, DefaultSettingSerializer>(serviceCollection);
    }

    [Fact]
    public void AddSettingsLibrary_WhenCalledTwice_Throws()
    {
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddSettingsLibrary();

        Assert.Throws<InvalidOperationException>(() => serviceCollection.AddSettingsLibrary());

        Assert.Single(serviceCollection, x => x.ServiceType == typeof(ISettingSerializer));
    }

    #endregion
}
