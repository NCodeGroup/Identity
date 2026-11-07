// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Management.Authorization;

/// <summary>
/// Describes the outcome of granting a role to a principal at a resource node.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class RoleAssignmentGrant
{
    /// <summary>
    /// Gets the role assignment the principal holds at the resource node, or <see langword="null"/> when the grant is
    /// refused.
    /// </summary>
    public PersistedRoleAssignment? Assignment { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assignment was added by the grant. The value is <see langword="false"/>
    /// when the principal already held the role at the node and the existing assignment was returned unchanged.
    /// </summary>
    [MemberNotNullWhen(true, nameof(Assignment))]
    public required bool IsAdded { get; init; }

    /// <summary>
    /// Gets a value indicating whether the grant was refused because the principal is a client that cannot hold the
    /// role at the node.
    /// </summary>
    // See ADR-1467.
    [MemberNotNullWhen(false, nameof(Assignment))]
    public bool IsRefused => Assignment is null;
}
