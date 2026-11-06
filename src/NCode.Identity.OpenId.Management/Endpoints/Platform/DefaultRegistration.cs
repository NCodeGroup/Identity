// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Management.Endpoints.Clients;
using NCode.Identity.OpenId.Management.Endpoints.Teams;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Platform;

/// <summary>
/// Provides extension methods to configure services and handlers for the management platform endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the management platform endpoint and the services it depends on.
        /// </summary>
        public void AddPlatformEndpoint()
        {
            builder.AddEndpoint<PlatformApiEndpointHandler>();
            builder.ServiceCollection.TryAddSingleton<
                ITenantClientCoherenceChecker,
                DefaultTenantClientCoherenceChecker
            >();
            builder.ServiceCollection.TryAddSingleton<
                ITeamClientCoherenceChecker,
                DefaultTeamClientCoherenceChecker
            >();
        }
    }
}
