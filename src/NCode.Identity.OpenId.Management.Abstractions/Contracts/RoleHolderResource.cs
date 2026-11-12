// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts;

/// <summary>
/// Represents a principal that holds one role at a node of the resource hierarchy: an item of a role list and the
/// answer to a grant of the role.
/// </summary>
// See ADR-1465.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class RoleHolderResource
{
    /// <summary>
    /// Gets the opaque identifier of the role assignment.
    /// </summary>
    public required string AssignmentId { get; init; }

    /// <summary>
    /// Gets the identifier of the principal that holds the role.
    /// </summary>
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the name of the granted role.
    /// </summary>
    public required string RoleName { get; init; }
}
