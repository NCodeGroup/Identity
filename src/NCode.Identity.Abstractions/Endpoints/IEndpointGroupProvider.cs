#region Copyright Preamble

// Copyright @ 2025 NCode Group
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
/// Provides the ability for a family of endpoints to configure themselves as a single group, typically by
/// creating a <see cref="RouteGroupBuilder"/> and applying shared conventions (such as exception handling)
/// before mapping its individual <see cref="IEndpointProvider"/> children into that group.
/// </summary>
[PublicAPI]
public interface IEndpointGroupProvider
{
    /// <summary>
    /// Maps a group of related identity endpoints into the <see cref="IEndpointRouteBuilder"/>.
    /// </summary>
    /// <param name="endpoints">The root <see cref="IEndpointRouteBuilder"/> instance.</param>
    void Map(IEndpointRouteBuilder endpoints);
}
