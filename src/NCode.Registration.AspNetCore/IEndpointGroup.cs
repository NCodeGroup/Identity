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

using JetBrains.Annotations;
using Microsoft.AspNetCore.Routing;

namespace NCode.Registration.AspNetCore;

/// <summary>
/// Declares a node in the endpoint route-group hierarchy: its relative <see cref="Prefix"/>, the endpoints, child
/// groups, and services it registers at startup (via <see cref="ConfigureServices"/>), and the conventions applied to
/// its <see cref="RouteGroupBuilder"/> when routes are mapped (via <see cref="ConfigureRoutes"/>). A group owns its own
/// contents: registering a root group recursively registers the whole subtree, and the route builder materializes each
/// group parent-first and maps every endpoint registered under it, so a prefix, its shared filters, and its children
/// are all declared in one place.
/// </summary>
[PublicAPI]
public interface IEndpointGroup
{
    /// <summary>
    /// Gets the unique name of the group; endpoints and child groups are registered under this name.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the relative route prefix for the group (for example, <c>/api</c> or <c>/tenants/{tenantId}</c>). An empty
    /// prefix groups endpoints for shared conventions without altering their routes.
    /// </summary>
    string Prefix { get; }

    /// <summary>
    /// Applies the group's shared conventions (such as endpoint filters) to its <see cref="RouteGroupBuilder"/>. The
    /// default implementation applies none.
    /// </summary>
    /// <param name="group">The <see cref="RouteGroupBuilder"/> created for this group.</param>
    void ConfigureRoutes(RouteGroupBuilder group) { }

    /// <summary>
    /// Registers the group's contents at startup via the supplied <see cref="IEndpointGroupBuilder"/>: the endpoints
    /// (<see cref="IEndpointGroupBuilder.AddEndpoint{T}"/>) and child groups (<see cref="IEndpointGroupBuilder.AddGroup{T}"/>)
    /// it contains, plus the services those endpoints depend on (the builder is itself an <see cref="IServiceBuilder"/>).
    /// The default implementation registers none.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointGroupBuilder"/> scoped to this group.</param>
    void ConfigureServices(IEndpointGroupBuilder builder) { }
}
