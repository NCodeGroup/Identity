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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Mediator;
using NCode.Registration.AspNetCore;

namespace NCode.Identity.OpenId.Management.Endpoints.Capabilities;

/// <summary>
/// Provides extension methods to configure the management capabilities discovery endpoint.
/// </summary>
internal static class DefaultRegistration
{
    extension(IEndpointGroupBuilder builder)
    {
        /// <summary>
        /// Registers the management capabilities discovery endpoint and its built-in capability providers.
        /// </summary>
        public void AddCapabilitiesEndpoint()
        {
            builder.AddEndpoint<CapabilitiesApiEndpointHandler>();

            // Capability providers compose the discovery document (ADR-0067); applications can register more.
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<DiscoverManagementCapabilitiesCommand>,
                    SecretTypeCapabilitiesProvider
                >()
            );
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<
                    ICommandHandler<DiscoverManagementCapabilitiesCommand>,
                    EccCurveCapabilitiesProvider
                >()
            );
        }
    }
}
