// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.PushedAuthorization;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId pushed authorization request endpoint
/// (RFC 9126).
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId pushed authorization request endpoint.
        /// </summary>
        public void AddPushedAuthorizationRequestEndpoint()
        {
            builder.AddEndpoint<DefaultPushedAuthorizationRequestEndpointHandler>();

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                IPushedAuthorizationRequestService,
                DefaultPushedAuthorizationRequestService
            >();
        }
    }
}
