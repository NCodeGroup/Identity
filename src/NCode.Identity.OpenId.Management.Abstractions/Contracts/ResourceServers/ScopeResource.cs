// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
