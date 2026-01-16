#region Copyright Preamble

// Copyright @ 2024 NCode Group
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
using NCode.Identity.OpenId.Authentication.Clients.Handlers;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides extension methods to configure services and handlers for OpenId Client services.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationLibrary"/>.</param>
    extension(IServiceBuilder<OpenIdAuthenticationLibrary> builder)
    {
        /// <summary>
        /// Configures services and handlers for OpenId Client services.
        /// </summary>
        /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
        [PublicAPI]
        public IServiceBuilder<OpenIdAuthenticationLibrary> AddClientServices()
        {
            var serviceCollection = builder.ServiceCollection;

            serviceCollection.TryAddSingleton<
                IOpenIdClientFactory,
                DefaultOpenIdClientFactory
            >();

            serviceCollection.TryAddSingleton<
                IClientAuthenticationService,
                DefaultClientAuthenticationService
            >();

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IClientAuthenticationHandler, ClientSecretBasicClientAuthenticationHandler>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IClientAuthenticationHandler, ClientSecretPostClientAuthenticationHandler>()
            );

            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IClientAuthenticationHandler, NoneClientAuthenticationHandler>()
            );

            return builder;
        }
    }
}
