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

namespace NCode.Identity.Endpoints;

/// <summary>
/// Declares a node in the endpoint route-group hierarchy: its relative <see cref="Prefix"/>, the conventions applied to
/// its <see cref="RouteGroupBuilder"/> (via <see cref="Configure"/>), and the endpoints and child groups it contains
/// (via <see cref="Build"/>). A group owns its own contents: registering a root group recursively registers the whole
/// subtree, and the route builder materializes each group parent-first and maps every endpoint registered under it, so
/// a prefix, its shared filters, and its children are all declared in one place.
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
    void Configure(RouteGroupBuilder group) { }

    /// <summary>
    /// Declares the endpoints and child groups contained by this group via the supplied
    /// <see cref="IEndpointGroupBuilder"/>. The default implementation declares none.
    /// </summary>
    /// <param name="builder">The <see cref="IEndpointGroupBuilder"/> scoped to this group.</param>
    void Build(IEndpointGroupBuilder builder) { }
}
