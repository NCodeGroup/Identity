// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Tenants;

/// <summary>
/// Provides extension methods to configure services and handlers for the management Tenants endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the management Tenants endpoint and the services it depends on.
        /// </summary>
        public void AddTenantEndpoint()
        {
            builder.AddEndpoint<TenantApiEndpointHandler>();
            builder.AddEndpoint<TenantRemoteSecretApiEndpointHandler>();
            builder.ServiceCollection.TryAddSingleton<ITenantValidator, DefaultTenantValidator>();
            builder.ServiceCollection.TryAddSingleton<
                ITenantClientCoherenceChecker,
                DefaultTenantClientCoherenceChecker
            >();
        }
    }
}
