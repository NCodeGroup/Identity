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
    /// Gets the role assignment the principal holds at the resource node.
    /// </summary>
    public required PersistedRoleAssignment Assignment { get; init; }

    /// <summary>
    /// Gets a value indicating whether the assignment was added by the grant. The value is <see langword="false"/>
    /// when the principal already held the role at the node and the existing assignment was returned unchanged.
    /// </summary>
    public required bool IsAdded { get; init; }
}
