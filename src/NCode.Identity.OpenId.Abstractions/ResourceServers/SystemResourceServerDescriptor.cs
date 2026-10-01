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
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.ResourceServers;

/// <summary>
/// Describes a reserved, system-owned resource server (such as the OpenID Connect identity API or the management
/// API) that is seeded into a tenant so that it is self-contained.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class SystemResourceServerDescriptor
{
    /// <summary>
    /// Gets the audience identifier of the resource server (for example, <c>urn:ncode:openid</c>).
    /// </summary>
    public required string Identifier { get; init; }

    /// <summary>
    /// Gets the human-readable name of the resource server.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the deployment plane into which this resource server is seeded. The default is
    /// <see cref="SystemResourceServerPlane.Tenant"/> (seeded into every tenant);
    /// <see cref="SystemResourceServerPlane.Control"/> is seeded only into the root tenant.
    /// </summary>
    public SystemResourceServerPlane Plane { get; init; } = SystemResourceServerPlane.Tenant;

    /// <summary>
    /// Gets the scopes owned by the resource server.
    /// </summary>
    public required IReadOnlyList<SystemScopeDescriptor> Scopes { get; init; }
}
