// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Registration;

namespace NCode.Identity.OpenId.Authentication.ResourceServers;

/// <summary>
/// Provides extension methods to configure the resource-server and scope resolution services.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the client-scope, scope-description, and resource-server credential resolution services.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddResourceServerServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<IClientScopeService, DefaultClientScopeService>();
            serviceCollection.TryAddSingleton<
                IScopeDescriptionService,
                DefaultScopeDescriptionService
            >();
            serviceCollection.TryAddSingleton<
                IResourceServerCredentialResolver,
                DefaultResourceServerCredentialResolver
            >();

            return builder;
        }
    }
}
