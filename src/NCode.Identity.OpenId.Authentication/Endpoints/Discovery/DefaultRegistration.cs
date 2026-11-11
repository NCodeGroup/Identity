// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Handlers;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Discovery;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId Discovery endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId Discovery endpoint.
        /// </summary>
        public void AddDiscoveryEndpoint()
        {
            builder.AddEndpoint<DefaultDiscoveryEndpointHandler>();

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<DiscoverMetadataCommand>,
                    DefaultDiscoverMetadataHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<DiscoverMetadataCommand>,
                    DefaultDiscoverScopesHandler
                >()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<DiscoverMetadataCommand>,
                    DefaultDiscoverSessionManagementHandler
                >()
            );
        }
    }
}
