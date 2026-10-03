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

namespace NCode.Identity.Endpoints;

/// <summary>
/// Provides the default implementation of <see cref="IEndpointGroupBuilder"/> that registers a group's endpoints as
/// keyed <see cref="IEndpointProvider"/> services and its child groups as keyed <see cref="IEndpointGroup"/> services,
/// both keyed by the owning group's <see cref="IEndpointGroup.Name"/>, and recurses into each child group's
/// <see cref="IEndpointGroup.Build"/>.
/// </summary>
internal sealed class EndpointGroupBuilder(IServiceCollection serviceCollection, string groupName)
    : IEndpointGroupBuilder
{
    /// <inheritdoc />
    public void AddEndpoint<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
    >()
        where T : class, IEndpointProvider
    {
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.KeyedSingleton<IEndpointProvider, T>(groupName)
        );
    }

    /// <inheritdoc />
    public void AddGroup<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicConstructors)] T
    >()
        where T : class, IEndpointGroup, new()
    {
        var child = new T();
        serviceCollection.TryAddEnumerable(
            ServiceDescriptor.KeyedSingleton<IEndpointGroup>(groupName, child)
        );
        child.Build(new EndpointGroupBuilder(serviceCollection, child.Name));
    }
}
