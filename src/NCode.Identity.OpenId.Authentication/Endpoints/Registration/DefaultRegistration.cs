#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.OpenId.Authentication.Endpoints.Registration.Logic;
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
                .ConfigurePrimaryHttpMessageHandler(() =>
                    new SocketsHttpHandler
                    {
                        AllowAutoRedirect = false,
                        ConnectCallback = SectorIdentifierHttpGuard.ConnectAsync,
                        AutomaticDecompression = DecompressionMethods.All,
                    }
                );
        }
    }
}
