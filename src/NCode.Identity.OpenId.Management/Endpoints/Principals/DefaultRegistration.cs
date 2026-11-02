// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Principals;

/// <summary>
/// Provides extension methods to configure services and handlers for the management Principals endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the management Principals endpoint.
        /// </summary>
        public void AddPrincipalEndpoint()
        {
            builder.AddEndpoint<PrincipalApiEndpointHandler>();
        }
    }
}
