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
using NCode.Identity.Endpoints;
using NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Discovery.Handlers;
using NCode.Mediator;
using NCode.Registration;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Discovery;

/// <summary>
/// Provides extension methods to configure services and handlers for the OpenId Discovery endpoint.
/// </summary>
[PublicAPI]
public static class DefaultRegistration
{
    /// <summary>
    /// Configures services and handlers for the OpenId Discovery endpoint.
    /// </summary>
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure services for <see cref="OpenIdAuthenticationEndpoints"/>.</param>
    /// <returns>The <see cref="IServiceBuilder{T}"/> instance for method chaining.</returns>
    public static IServiceBuilder<OpenIdAuthenticationEndpoints> AddDiscoveryEndpoint(
        this IServiceBuilder<OpenIdAuthenticationEndpoints> builder)
    {
        builder.AddEndpointProvider<DefaultDiscoveryEndpointHandler>();

        var serviceCollection = builder.ServiceCollection;

        serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<
            ICommandHandler<DiscoverMetadataCommand>,
            DefaultDiscoverMetadataHandler>());

        return builder;
    }
}
