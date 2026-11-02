// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Registration;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// Provides extension methods to configure services and handlers for OpenId Authentication endpoints.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder{T}"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures services and handlers for OpenId Authentication endpoints.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddEndpoints()
        {
            var newBuilder = builder.NewBuilder<OpenIdAuthenticationEndpoints>();

            newBuilder.AddEndpointGroup<OpenIdEndpointGroup>();

            return builder;
        }
    }
}
