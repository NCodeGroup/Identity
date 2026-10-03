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
/// Declares a named node in the endpoint route-group hierarchy: its relative <see cref="Prefix"/>, its optional
/// <see cref="ParentName"/>, and the conventions (such as endpoint filters) applied to the resulting
/// <see cref="RouteGroupBuilder"/>. The route builder materializes each group parent-first and maps every
/// <see cref="IEndpointProvider"/> registered under the group's <see cref="Name"/> into it, so a prefix and its shared
/// filters are declared exactly once and child families inherit them.
/// </summary>
[PublicAPI]
public interface IEndpointGroup
{
    /// <summary>
    /// Gets the unique name of the group; this is the key under which an <see cref="IEndpointProvider"/> is registered
    /// to be mapped into the group.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the <see cref="Name"/> of the parent group this group nests under, or <c>null</c> when the group is rooted
    /// directly on the top-level endpoint route builder.
    /// </summary>
    string? ParentName { get; }

    /// <summary>
    /// Gets the relative route prefix for the group (for example, <c>/api</c> or <c>/tenants/{tenantId}</c>). An empty
    /// prefix groups endpoints for shared conventions without altering their routes.
    /// </summary>
    string Prefix { get; }

    /// <summary>
    /// Applies the group's shared conventions (such as endpoint filters) to the created <see cref="RouteGroupBuilder"/>.
    /// </summary>
    /// <param name="group">The <see cref="RouteGroupBuilder"/> created for this group.</param>
    void Configure(RouteGroupBuilder group);
}
