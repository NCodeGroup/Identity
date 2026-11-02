// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.CheckSession;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenID Connect Session Management 1.0
/// <c>check_session_iframe</c> endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures the OpenID Connect Session Management 1.0 <c>check_session_iframe</c> endpoint. The endpoint is
        /// always mapped but is only advertised in discovery when the <c>session_management_supported</c> setting is
        /// enabled.
        /// </summary>
        public void AddCheckSessionEndpoint()
        {
            builder.AddEndpoint<DefaultCheckSessionEndpointProvider>();
        }
    }
}
