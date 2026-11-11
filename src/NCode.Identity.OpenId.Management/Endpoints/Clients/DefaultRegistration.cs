// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Clients;

/// <summary>
/// Provides extension methods to configure services and handlers for the management Clients endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the management Clients endpoint and the services it depends on.
        /// </summary>
        public void AddClientEndpoint()
        {
            builder.AddEndpoint<ClientApiEndpointHandler>();
            builder.AddClientServices();
        }

        /// <summary>
        /// Registers the routes that create and list machine clients and the services they depend on.
        /// </summary>
        public void AddMachineClientEndpoint()
        {
            builder.AddEndpoint<MachineClientEndpointProvider>();
            builder.ServiceCollection.TryAddSingleton<ClientApiEndpointHandler>();
            builder.AddClientServices();
        }

        private void AddClientServices()
        {
            builder.ServiceCollection.TryAddSingleton<IClientValidator, DefaultClientValidator>();
            builder.ServiceCollection.TryAddSingleton<
                IHostManagedClientChecker,
                DefaultHostManagedClientChecker
            >();
            builder.ServiceCollection.TryAddSingleton<
                IClientKeySourceChecker,
                DefaultClientKeySourceChecker
            >();
        }
    }
}
