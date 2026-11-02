// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
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

            // The sector_identifier_uri fetch (OpenID Connect Core 1.0 §8.1) uses a dedicated HTTP client whose
            // connections are pinned away from private/loopback/link-local addresses (SSRF floor) and which never
            // follows redirects, so a redirect cannot bounce the fetch to an internal host.
            serviceCollection
                .AddHttpClient(DefaultSectorIdentifierValidator.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(serviceProvider => new SocketsHttpHandler
                {
                    AllowAutoRedirect = false,
                    ConnectCallback = serviceProvider
                        .GetRequiredService<IOutboundHttpForgeryGuard>()
                        .ConnectAsync,
                    AutomaticDecompression = DecompressionMethods.All,
                });
        }
    }
}
