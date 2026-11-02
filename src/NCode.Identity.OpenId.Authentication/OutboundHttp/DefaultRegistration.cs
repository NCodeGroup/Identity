// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.OutboundHttp;

/// <summary>
/// Provides extension methods to configure the shared, SSRF-guarded outbound HTTP services used by every
/// server-initiated fetch of, or delivery to, a client-supplied URL.
/// </summary>
internal static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures the outbound HTTP forgery guard and the capped response-body reader.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddOutboundHttpServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.AddHttpClient();
            serviceCollection.TryAddSingleton<
                IOutboundHttpForgeryGuard,
                DefaultOutboundHttpForgeryGuard
            >();
            serviceCollection.TryAddSingleton<
                IOutboundHttpContentReader,
                DefaultOutboundHttpContentReader
            >();

            return builder;
        }
    }
}
