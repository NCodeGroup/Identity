// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Grants;

/// <summary>
/// Provides extension methods to configure services and handlers for the management Grants endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the management Grants endpoint.
        /// </summary>
        public void AddGrantEndpoint()
        {
            builder.AddEndpoint<GrantApiEndpointHandler>();
        }
    }
}
