// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Servers;

/// <summary>
/// Provides extension methods to configure services and handlers for the management Servers endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the management Servers endpoint and the services it depends on.
        /// </summary>
        public void AddServerEndpoint()
        {
            builder.AddEndpoint<ServerApiEndpointHandler>();
            builder.ServiceCollection.TryAddSingleton<IServerValidator, DefaultServerValidator>();
            builder.ServiceCollection.TryAddSingleton<
                ITenantClientCoherenceChecker,
                DefaultTenantClientCoherenceChecker
            >();
        }
    }
}
