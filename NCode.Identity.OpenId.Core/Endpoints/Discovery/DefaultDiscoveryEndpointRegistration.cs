#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using NCode.Identity.OpenId.Endpoints.Discovery.Commands;
using NCode.Identity.OpenId.Mediator;

namespace NCode.Identity.OpenId.Endpoints.Discovery;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register required services and handlers for the discovery endpoint.
/// </summary>
[PublicAPI]
public static class DefaultDiscoveryEndpointRegistration
{
    /// <summary>
    /// Registers the required services and handlers for the discovery endpoint into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="serviceCollection">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddDiscoveryEndpoint(this IServiceCollection serviceCollection)
    {
        serviceCollection.AddSingleton<DefaultDiscoveryEndpointHandler>();

        serviceCollection.TryAddSingleton<IOpenIdEndpointProvider>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultDiscoveryEndpointHandler>());

        serviceCollection.TryAddSingleton<ICommandHandler<DiscoverMetadataCommand>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultDiscoveryEndpointHandler>());

        return serviceCollection;
    }
}
