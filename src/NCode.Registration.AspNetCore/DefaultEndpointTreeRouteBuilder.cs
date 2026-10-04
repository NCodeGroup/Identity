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

using System.Collections.Immutable;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace NCode.Registration.AspNetCore;

/// <summary>
/// Provides a default implementation of the <see cref="IEndpointTreeRouteBuilder"/> abstraction.
/// </summary>
internal class DefaultEndpointTreeRouteBuilder(
    IEnumerable<IEndpointGroup> endpointGroups,
    IEnumerable<IEndpointProvider> endpointProviders
) : IEndpointTreeRouteBuilder
{
    private ImmutableArray<IEndpointGroup> EndpointGroups { get; } = [.. endpointGroups];

    private ImmutableArray<IEndpointProvider> EndpointProviders { get; } = [.. endpointProviders];

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        var serviceProvider = endpoints.ServiceProvider;

        // Each root group owns its subtree: it is materialized, then its endpoints and child groups are mapped into it
        // recursively (both resolved keyed by the group's name).
        foreach (var group in EndpointGroups)
        {
            MapGroup(group, endpoints, serviceProvider);
        }

        // Ungrouped providers map directly onto the root.
        foreach (var endpointProvider in EndpointProviders)
        {
            endpointProvider.Map(endpoints);
        }
    }

    private static void MapGroup(
        IEndpointGroup group,
        IEndpointRouteBuilder parent,
        IServiceProvider serviceProvider
    )
    {
        var routeGroup = parent.MapGroup(group.Prefix);
        group.ConfigureRoutes(routeGroup);

        foreach (
            var endpointProvider in serviceProvider.GetKeyedServices<IEndpointProvider>(group.Name)
        )
        {
            endpointProvider.Map(routeGroup);
        }

        foreach (var childGroup in serviceProvider.GetKeyedServices<IEndpointGroup>(group.Name))
        {
            MapGroup(childGroup, routeGroup, serviceProvider);
        }
    }
}
