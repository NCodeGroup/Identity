// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Management.Promotions;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

/// <summary>
/// Provides extension methods to configure services and handlers for the management Teams endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the management Teams endpoint and the services it depends on.
        /// </summary>
        public void AddTeamEndpoint()
        {
            builder.AddEndpoint<TeamApiEndpointHandler>();
            builder.AddEndpoint<TenantPromotionApiEndpointHandler>();
            builder.ServiceCollection.TryAddSingleton<ITeamValidator, DefaultTeamValidator>();
            builder.AddTenantPromotionServices();
        }
    }
}
