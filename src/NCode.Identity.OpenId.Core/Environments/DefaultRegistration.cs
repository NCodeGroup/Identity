// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Registration;

namespace NCode.Identity.OpenId.Environments;

/// <summary>
/// Provides extension methods to configure services and handlers for <see cref="OpenIdCoreLibrary"/>.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdCoreLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdCoreLibrary> builder)
    {
        /// <summary>
        /// Configures services and handlers for <see cref="OpenIdCoreLibrary"/>.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdCoreLibrary> AddEnvironmentServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                IOpenIdEnvironmentFactory,
                DefaultOpenIdEnvironmentFactory
            >();
            serviceCollection.TryAddSingleton<
                IOpenIdEnvironmentProvider,
                DefaultOpenIdEnvironmentProvider
            >();

            return builder;
        }
    }
}
