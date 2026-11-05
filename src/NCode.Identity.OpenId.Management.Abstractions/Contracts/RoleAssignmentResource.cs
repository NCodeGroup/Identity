// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts;

/// <summary>
/// Represents a role granted to a principal at a node of the resource hierarchy.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class RoleAssignmentResource
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

    /// <summary>
    /// Gets the type of the node at which the role is granted, such as <c>platform</c>, <c>team</c>, <c>tenant</c> or
    /// <c>client</c>.
    /// </summary>
    public required string ResourceType { get; init; }

    /// <summary>
    /// Gets the identifier of the node at which the role is granted.
    /// </summary>
    public required string ResourceId { get; init; }

    /// <summary>
    /// Gets the identifier of the tenant that the role assignment belongs to.
    /// </summary>
    public required string TenantId { get; init; }
}
