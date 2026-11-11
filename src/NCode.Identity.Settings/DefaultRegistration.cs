// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.Registration;

namespace NCode.Identity.Settings;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register the settings services.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    extension(IServiceCollection serviceCollection)
    {
        /// <summary>
        /// Registers the default setting collection providers, factories and serializer, minting the
        /// <see cref="SettingsLibrary"/> marker that the libraries built on settings require.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> for the settings library so that calls can be chained.</returns>
        [PublicAPI]
        public IServiceBuilder<SettingsLibrary> AddSettingsLibrary()
        {
            ArgumentNullException.ThrowIfNull(serviceCollection);

            var newBuilder = serviceCollection.NewBuilder<SettingsLibrary>();

            serviceCollection.AddCollectionProviders();

            serviceCollection.TryAddSingleton<
                ISettingDescriptorCollectionProvider,
                DefaultSettingDescriptorCollectionProvider
            >();
            serviceCollection.TryAddSingleton<
                ISettingScopeCollectionProvider,
                DefaultSettingScopeCollectionProvider
            >();
            serviceCollection.TryAddSingleton<
                IReadOnlySettingCollectionProviderFactory,
                DefaultReadOnlySettingCollectionProviderFactory
            >();
            serviceCollection.TryAddSingleton<
                ISettingCollectionFactory,
                DefaultSettingCollectionFactory
            >();
            serviceCollection.TryAddSingleton<ISettingSerializer, DefaultSettingSerializer>();

            return newBuilder;
        }
    }
}
