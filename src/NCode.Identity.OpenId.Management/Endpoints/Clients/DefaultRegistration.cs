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
        }
    }
}
