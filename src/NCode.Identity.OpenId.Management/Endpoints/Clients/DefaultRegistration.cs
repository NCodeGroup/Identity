// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Registration.AspNetCore;

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

        /// <summary>
        /// Registers the routes that create and list machine clients and the services they depend on.
        /// </summary>
        public void AddMachineClientEndpoint()
        {
            builder.AddEndpoint<MachineClientEndpointProvider>();
            builder.ServiceCollection.TryAddSingleton<ClientApiEndpointHandler>();
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
