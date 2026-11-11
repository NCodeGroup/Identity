// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

/// <summary>
/// Provides extension methods to configure the management capabilities discovery endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the management capabilities discovery endpoint and its built-in capability providers.
        /// </summary>
        public void AddCapabilitiesEndpoint()
        {
            builder.AddEndpoint<CapabilitiesApiEndpointHandler>();

            // Capability providers compose the discovery document (ADR-1067); applications can register more.
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<DiscoverManagementCapabilitiesCommand>,
                    SecretTypeCapabilitiesProvider
                >()
            );
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<DiscoverManagementCapabilitiesCommand>,
                    EccCurveCapabilitiesProvider
                >()
            );
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<DiscoverManagementCapabilitiesCommand>,
                    AlgorithmCapabilitiesProvider
                >()
            );
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<DiscoverManagementCapabilitiesCommand>,
                    RemoteKeyProviderCapabilitiesProvider
                >()
            );
        }
    }
}
