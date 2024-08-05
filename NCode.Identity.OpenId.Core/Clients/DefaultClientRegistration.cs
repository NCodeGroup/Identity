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
using NCode.Identity.OpenId.Clients.Handlers;

namespace NCode.Identity.OpenId.Clients;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register core client services and handlers.
/// </summary>
[PublicAPI]
public static class DefaultClientRegistration
{
    /// <summary>
    /// Registers core client services and handlers into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddClientServices(this IServiceCollection serviceCollection)
    {
        serviceCollection.TryAddSingleton<
            IOpenIdClientFactory,
            DefaultOpenIdClientFactory>();

        serviceCollection.TryAddSingleton<
            IClientAuthenticationService,
            DefaultClientAuthenticationService>();

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            IClientAuthenticationHandler,
            BasicClientAuthenticationHandler>());

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            IClientAuthenticationHandler,
            RequestBodyClientAuthenticationHandler>());

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            IClientAuthenticationHandler,
            RequestQueryClientAuthenticationHandler>());

        return serviceCollection;
    }
}
