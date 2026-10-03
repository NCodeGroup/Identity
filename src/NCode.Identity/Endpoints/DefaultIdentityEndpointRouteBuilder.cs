#region Copyright Preamble

// Copyright @ 2023 NCode Group
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

namespace NCode.Identity.Endpoints;

/// <summary>
/// Provides a default implementation of the <see cref="IIdentityEndpointRouteBuilder"/> abstraction.
/// </summary>
internal class DefaultIdentityEndpointRouteBuilder(
    IEnumerable<IEndpointGroup> endpointGroups,
    IEnumerable<IEndpointProvider> endpointProviders
) : IIdentityEndpointRouteBuilder
{
    private ImmutableArray<IEndpointGroup> EndpointGroups { get; } = [.. endpointGroups];

    private ImmutableArray<IEndpointProvider> EndpointProviders { get; } = [.. endpointProviders];

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        // Named groups own their route group and conventions; each is materialized parent-first and its keyed
        // IEndpointProvider children are mapped into it.
        MapNamedGroups(endpoints);

        // Ungrouped providers map directly onto the root.
        foreach (var endpointProvider in EndpointProviders)
        {
            endpointProvider.Map(endpoints);
        }
    }

    private void MapNamedGroups(IEndpointRouteBuilder endpoints)
    {
        if (EndpointGroups.IsEmpty)
        {
            return;
        }

        var serviceProvider = endpoints.ServiceProvider;
        var groupsByName = EndpointGroups.ToDictionary(group => group.Name, StringComparer.Ordinal);
        var built = new Dictionary<string, RouteGroupBuilder>(StringComparer.Ordinal);

        RouteGroupBuilder Build(string name)
        {
            if (built.TryGetValue(name, out var existing))
            {
                return existing;
            }

            var group = groupsByName[name];
            IEndpointRouteBuilder parent = group.ParentName is null
                ? endpoints
                : Build(group.ParentName);

            var routeGroup = parent.MapGroup(group.Prefix);
            group.Configure(routeGroup);
            built[name] = routeGroup;
            return routeGroup;
        }

        foreach (var group in EndpointGroups)
        {
            var routeGroup = Build(group.Name);
            foreach (
                var endpointProvider in serviceProvider.GetKeyedServices<IEndpointProvider>(
                    group.Name
                )
            )
            {
                endpointProvider.Map(routeGroup);
            }
        }
    }
}
