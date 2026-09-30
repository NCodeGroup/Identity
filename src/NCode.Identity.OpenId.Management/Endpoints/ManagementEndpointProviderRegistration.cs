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

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Registration;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// Provides extension methods to register management API endpoint providers.
/// </summary>
internal static class ManagementEndpointProviderRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure.</param>
    extension(IServiceBuilder builder)
    {
        /// <summary>
        /// Registers an <see cref="IManagementEndpointProvider"/> implementation so that it is mapped into the
        /// shared <c>/api</c> route group by the <see cref="ManagementEndpointGroupProvider"/> instead of directly
        /// onto the root <see cref="NCode.Identity.Endpoints.IEndpointProvider"/> collection.
        /// </summary>
        public void AddManagementEndpointProvider<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
        >()
            where T : class, IManagementEndpointProvider
        {
            var serviceCollection = builder.ServiceCollection;
            serviceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IManagementEndpointProvider, T>()
            );
        }
    }
}
