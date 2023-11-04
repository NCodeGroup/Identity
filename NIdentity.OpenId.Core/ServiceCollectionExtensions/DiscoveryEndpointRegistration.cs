﻿#region Copyright Preamble

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

using Microsoft.Extensions.DependencyInjection;
using NIdentity.OpenId.Endpoints;
using NIdentity.OpenId.Endpoints.Discovery;
using NIdentity.OpenId.Endpoints.Discovery.Commands;
using NIdentity.OpenId.Endpoints.Discovery.Results;
using NIdentity.OpenId.Mediator;
using NIdentity.OpenId.Results;

namespace NIdentity.OpenId.ServiceCollectionExtensions;

/// <summary>
/// Provides extension methods for <see cref="IServiceCollection"/> to register required services and handlers for the discovery endpoint.
/// </summary>
public static class DiscoveryEndpointRegistration
{
    /// <summary>
    /// Registers the required services and handlers for the discovery endpoint into the provided <see cref="IServiceCollection"/> instance.
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> instance.</param>
    /// <returns>The <see cref="IServiceCollection"/> instance for method chaining.</returns>
    public static IServiceCollection AddDiscoveryEndpoint(this IServiceCollection services)
    {
        services.AddSingleton<IOpenIdEndpointProvider, DefaultDiscoveryEndpointProvider>();

        services.AddSingleton<DefaultDiscoveryEndpointHandler>();

        services.AddSingleton<ICommandResponseHandler<DiscoveryEndpointCommand, IOpenIdResult>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultDiscoveryEndpointHandler>());

        services.AddSingleton<ICommandHandler<DiscoverMetadataCommand>>(serviceProvider =>
            serviceProvider.GetRequiredService<DefaultDiscoveryEndpointHandler>());

        services.AddSingleton<IOpenIdResultExecutor<DiscoveryResult>, DiscoveryResultExecutor>();

        return services;
    }
}
