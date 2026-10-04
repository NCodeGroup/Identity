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
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NCode.Registration.AspNetCore;

/// <summary>
/// Provides extension methods to register endpoint providers and endpoint groups.
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
        public void AddEndpointProvider<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
        >()
            where T : class, IEndpointProvider
        {
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEndpointProvider, T>()
            );
        }

        /// <summary>
        /// Registers a root <see cref="IEndpointGroup"/> and recursively registers the endpoints and child groups it
        /// declares via <see cref="IEndpointGroup.ConfigureServices"/>. The root is mapped onto the top-level endpoint
        /// route builder; each child group nests under its parent and inherits the parent's conventions.
        /// </summary>
        public void AddEndpointGroup<
            [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
        >()
            where T : class, IEndpointGroup, new()
        {
            var root = new T();
            builder.ServiceCollection.TryAddEnumerable(
                ServiceDescriptor.Singleton<IEndpointGroup>(root)
            );
            root.ConfigureServices(new EndpointGroupBuilder(builder, root.Name));
        }
    }
}
