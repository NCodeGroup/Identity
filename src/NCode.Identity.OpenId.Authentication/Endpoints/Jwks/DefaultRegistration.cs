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

using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId <c>JSON Web Key Set (JWKS)</c> endpoint.
/// </summary>
[PublicAPI]
internal static class DefaultRegistration
{
    extension(IServiceBuilder<OpenIdAuthenticationEndpoints> builder)
    {
        /// <summary>
        /// Configures services and handlers for the OpenId <c>JSON Web Key Set (JWKS)</c> endpoint.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        public IServiceBuilder<OpenIdAuthenticationEndpoints> AddJwksEndpoint()
        {
            builder.AddOpenIdEndpointProvider<DefaultJwksEndpointHandler>();

            var serviceCollection = builder.ServiceCollection;

            // The registry is the single, discoverable source of truth for which EC curves are published.
            // Applications can register additional EccCurveSpecification services to extend the supported set.
            serviceCollection.TryAddSingleton<
                IEccCurveSpecificationRegistry,
                DefaultEccCurveSpecificationRegistry
            >();

            // Register the built-in converters. Applications can register additional IJsonWebKeyConverter
            // implementations to publish other secret key types without modifying the endpoint handler.
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IJsonWebKeyConverter, RsaJsonWebKeyConverter>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IJsonWebKeyConverter, EccJsonWebKeyConverter>()
            );

            return builder;
        }
    }
}
