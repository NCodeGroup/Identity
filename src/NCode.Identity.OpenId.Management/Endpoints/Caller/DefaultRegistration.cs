// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Caller;

/// <summary>
/// Provides extension methods to configure services and handlers for the management Caller endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the management Caller endpoint.
        /// </summary>
        public void AddCallerEndpoint()
        {
            builder.AddEndpoint<CallerApiEndpointHandler>();
        }
    }
}
