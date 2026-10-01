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

namespace NCode.Identity.OpenId.Management.Contracts.ResourceServers;

/// <summary>
/// Represents the REST resource for a scope (a permission, in Auth0 terms) owned by a resource server.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class ScopeResource
{
    /// <summary>
    /// Gets the value of this scope (for example, <c>read:messages</c>).
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// Gets the human-readable description for this scope.
    /// </summary>
    public required string? Description { get; init; }

    /// <summary>
    /// Gets a value indicating whether this scope is a reserved, system-owned scope that cannot be deleted.
    /// </summary>
    public required bool IsSystem { get; init; }
}
