// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Sessions;

/// <summary>
/// Provides extension methods to configure services and handlers for the management Sessions endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the management Sessions endpoint.
        /// </summary>
        public void AddSessionEndpoint()
        {
            builder.AddEndpoint<SessionApiEndpointHandler>();
        }
    }
}
