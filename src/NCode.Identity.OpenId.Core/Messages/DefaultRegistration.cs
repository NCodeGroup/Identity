// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.Registration;

namespace NCode.Identity.OpenId.Messages;

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
        public IServiceBuilder<OpenIdCoreLibrary> AddMessageServices()
        {
            builder.AddMessageFactory<DefaultOpenIdError>();
            builder.AddMessageFactory<OpenIdMessage>();

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<KnownParameter>,
                    DefaultCommonParameterDataSource
                >()
            );

            serviceCollection.TryAddSingleton<
                IKnownParameterCollectionProvider,
                DefaultKnownParameterCollectionProvider
            >();

            serviceCollection.TryAddSingleton<
                IOpenIdMessageFactorySelector,
                DefaultOpenIdMessageFactorySelector
            >();

            return builder;
        }
    }
}
