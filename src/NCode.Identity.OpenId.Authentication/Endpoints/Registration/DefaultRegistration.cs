// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Registration;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId dynamic client registration (RFC 7591)
/// and registration management (RFC 7592) endpoints.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId dynamic client registration and management endpoints.
        /// </summary>
        public void AddClientRegistrationEndpoint()
        {
            builder.AddEndpoint<DefaultClientRegistrationEndpointHandler>();

            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                IInitialAccessTokenValidator,
                DefaultInitialAccessTokenValidator
            >();

            serviceCollection.TryAddSingleton<
                ISectorIdentifierValidator,
                DefaultSectorIdentifierValidator
            >();
        }
    }
}
