// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Principals;
using NCode.Identity.Registration;

namespace NCode.Identity.OpenId.PrincipalResolution;

/// <summary>
/// Provides extension methods to configure services for principal resolution and federated-identity linking.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdCoreLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdCoreLibrary> builder)
    {
        /// <summary>
        /// Configures the principal-resolution and federated-identity linking services.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdCoreLibrary> AddPrincipalResolutionServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                IFederatedIdentityLinkingPolicy,
                DefaultFederatedIdentityLinkingPolicy
            >();

            serviceCollection.TryAddSingleton<IPrincipalResolver, DefaultPrincipalResolver>();

            serviceCollection.TryAddSingleton<
                IClientPrincipalResolver,
                DefaultClientPrincipalResolver
            >();

            // Subject-metadata projection resolves a principal's inlined metadata bags fresh at issuance (ADR-1055).
            serviceCollection.TryAddSingleton<
                IPrincipalMetadataProvider,
                DefaultPrincipalMetadataProvider
            >();

            return builder;
        }
    }
}
