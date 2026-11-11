// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Authentication.Messages.Commands;
using NCode.Identity.OpenId.Authentication.Messages.Handlers;
using NCode.Identity.OpenId.Authentication.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.Registration;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Messages;

/// <summary>
/// Provides extension methods to configure services and handlers for essential OpenId Messages.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the services and handlers for essential OpenId Messages.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddMessageServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICollectionDataSource<KnownParameter>,
                    DefaultAuthenticationParameterDataSource
                >()
            );

            serviceCollection.TryAddSingleton<
                ICommandResponseHandler<LoadRequestValuesCommand, IRequestValues>,
                DefaultLoadRequestValuesHandler
            >();

            return builder;
        }
    }
}
