// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Management.Contracts;

/// <summary>
/// Represents an owner of a resource: a role assignment that grants a principal authority over the resource and
/// everything beneath it.
/// </summary>
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class OwnerResource
{
    /// <summary>
    /// Gets the opaque identifier of the owner (role) assignment.
    /// </summary>
    public required string AssignmentId { get; init; }

    /// <summary>
    /// Gets the identifier of the principal that owns the resource.
    /// </summary>
    public required string PrincipalId { get; init; }

    /// <summary>
    /// Gets the name of the granted role.
    /// </summary>
    public required string RoleName { get; init; }
}
