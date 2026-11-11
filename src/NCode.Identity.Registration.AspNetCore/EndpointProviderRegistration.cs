// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace NCode.Identity.Registration.AspNetCore;

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
