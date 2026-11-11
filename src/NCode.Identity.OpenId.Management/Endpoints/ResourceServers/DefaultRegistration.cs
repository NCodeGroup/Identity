// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.ResourceServers;

/// <summary>
/// Provides extension methods to configure services and handlers for the management Resource Servers and Client Grants
/// endpoints.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the management Resource Servers endpoint and the services it depends on.
        /// </summary>
        public void AddResourceServerEndpoint()
        {
            builder.AddEndpoint<ResourceServerApiEndpointHandler>();
            builder.ServiceCollection.TryAddSingleton<
                IResourceServerValidator,
                DefaultResourceServerValidator
            >();
        }

        /// <summary>
        /// Registers the management Client Grants endpoint and the services it depends on.
        /// </summary>
        public void AddClientGrantEndpoint()
        {
            builder.AddEndpoint<ClientGrantApiEndpointHandler>();
            builder.ServiceCollection.TryAddSingleton<
                IClientGrantValidator,
                DefaultClientGrantValidator
            >();
        }
    }
}
