#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NCode.Registration;

namespace NCode.Identity.Endpoints;

// TODO

/// <summary>
/// Provides extension methods to configure identity endpoint providers.
/// </summary>
[PublicAPI]
public static class EndpointProviderRegistration
{
    /// <param name="builder">The <see cref="IServiceBuilder"/> to configure.</param>
    extension(IServiceBuilder builder)
    {
        /// <summary>
        /// Registers an <see cref="IEndpointProvider"/> implementation.
        /// </summary>
        public void AddEndpointProvider<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T>()
            where T : class, IEndpointProvider
        {
            var serviceCollection = builder.ServiceCollection;
            serviceCollection.TryAddEnumerable(ServiceDescriptor.Singleton<IEndpointProvider, T>());
        }
    }
}
