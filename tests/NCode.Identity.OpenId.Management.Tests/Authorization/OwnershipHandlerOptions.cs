// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Management;

/// <summary>
/// Provides the optional inputs of an authorization decision that a test asks the ownership handler for.
/// </summary>
internal sealed class OwnershipHandlerOptions
{
    /// <summary>
    /// Gets a value indicating whether the handler decides the requirement, so it reads the principal and the node
    /// chain.
    /// </summary>
    public bool Decides { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the handler reads the role assignments of the caller, which it does only for a
    /// caller with a principal.
    /// </summary>
    public bool ReadsAssignments { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether the handler reads the tenant, which it does not for a team or platform node.
    /// </summary>
    public bool ReadsTenant { get; init; } = true;

    /// <summary>
    /// Gets the identifier of the team that owns the tenant, or <see langword="null"/> for a tenant that is unknown.
    /// </summary>
    public string? TenantTeamId { get; init; } = OwnershipHandlerTests.TeamId;

    /// <summary>
    /// Gets the identifiers of the ancestors of the team that owns the tenant, or <see langword="null"/> for none.
    /// </summary>
    public string[]? TeamAncestors { get; init; }
}
