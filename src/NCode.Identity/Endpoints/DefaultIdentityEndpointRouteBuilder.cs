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
using Microsoft.AspNetCore.Routing;

namespace NCode.Identity.Endpoints;

/// <summary>
/// Provides a default implementation of the <see cref="IIdentityEndpointRouteBuilder"/> abstraction.
/// </summary>
internal class DefaultIdentityEndpointRouteBuilder(
    IEnumerable<IEndpointGroupProvider> endpointGroupProviders,
    IEnumerable<IEndpointProvider> endpointProviders
) : IIdentityEndpointRouteBuilder
{
    private ImmutableArray<IEndpointGroupProvider> EndpointGroupProviders { get; } =
    [.. endpointGroupProviders];

    private ImmutableArray<IEndpointProvider> EndpointProviders { get; } = [.. endpointProviders];

    /// <inheritdoc />
    public void Map(IEndpointRouteBuilder endpoints)
    {
        // Grouped families (e.g. OpenID) own their own route group and conventions.
        foreach (var endpointGroupProvider in EndpointGroupProviders)
        {
            endpointGroupProvider.Map(endpoints);
        }

        // Ungrouped providers map directly onto the root.
        foreach (var endpointProvider in EndpointProviders)
        {
            endpointProvider.Map(endpoints);
        }
    }
}
