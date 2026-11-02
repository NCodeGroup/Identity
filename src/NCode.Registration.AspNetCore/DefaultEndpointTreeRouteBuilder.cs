// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
        var prefix = group.GetPrefix(serviceProvider);
        var routeGroup = parent.MapGroup(prefix);
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
