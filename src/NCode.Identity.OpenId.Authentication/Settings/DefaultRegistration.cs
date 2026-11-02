// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Settings;

/// <summary>
/// Provides extension methods to contribute the authentication slice's protocol-specific setting descriptors to the
/// shared, composable setting-descriptor catalog.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Contributes the client-authentication-dependent setting descriptors to the catalog.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddClientSettingServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<SettingDescriptor>,
                    DefaultClientSettingDescriptorDataSource
                >()
            );

            // The FAPI 2.0 and FAPI 1.0 Advanced security profiles; the catalog (Core) resolves them from this strategy
            // collection.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ISecurityProfile, Fapi2SecurityProfile>()
            );
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<ISecurityProfile, Fapi1AdvancedSecurityProfile>()
            );

            return builder;
        }
    }
}
