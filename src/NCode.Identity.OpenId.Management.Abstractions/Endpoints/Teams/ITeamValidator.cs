// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Management.Contracts.Teams;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

/// <summary>
/// Validates the intrinsic core preconditions for team management operations. Register a custom implementation to
/// replace or extend the default validation.
/// </summary>
// See ADR-1411
[PublicAPI]
public interface ITeamValidator
{
    /// <summary>
    /// Authorizes the caller to create a team, at the platform or at the parent team the route names. It judges only
    /// route values and runs before any team is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="controlPlaneTenantId">The identifier of the tenant that serves the management request.</param>
    /// <param name="parentTeamId">The identifier of the parent team the route names, or <c>null</c> for a top-level team.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> (<c>401</c> or <c>403</c>) when the caller lacks the authority, or <c>null</c> when authorized.</returns>
    ValueTask<ManagementError?> AuthorizeCreateAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        string? parentTeamId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Authorizes the caller to update the specified team. It judges only route values and runs before the team is
    /// read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="controlPlaneTenantId">The identifier of the tenant that serves the management request.</param>
    /// <param name="teamId">The identifier of the team named by the route.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> (<c>401</c> or <c>403</c>) when the caller lacks the authority, or <c>null</c> when authorized.</returns>
    ValueTask<ManagementError?> AuthorizeUpdateAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        string teamId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Authorizes the caller to delete the specified team at the team's own node. It judges only route values and runs
    /// before the team is read.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="controlPlaneTenantId">The identifier of the tenant that serves the management request.</param>
    /// <param name="teamId">The identifier of the team named by the route.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> (<c>401</c> or <c>403</c>) when the caller lacks the authority, or <c>null</c> when authorized.</returns>
    ValueTask<ManagementError?> AuthorizeDeleteAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        string teamId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates the row of the team to be created. It does not authorize the route's node.
    /// </summary>
    /// <param name="team">The <see cref="PersistedTeam"/> to be created.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateCreateAsync(
        PersistedTeam team,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the specified team may be updated to the supplied proposed state. Disabling or restoring the
    /// team is judged here against the stored <see cref="PersistedTeam.IsDisabled"/>, authorized at the node above the
    /// stored team: the parent team for a child team and the platform for a top-level team.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="controlPlaneTenantId">The identifier of the tenant that serves the management request.</param>
    /// <param name="team">The current <see cref="PersistedTeam"/> being updated.</param>
    /// <param name="model">The proposed (patched) team metadata.</param>
    /// <param name="ifMatch">The optional <c>If-Match</c> precondition: <c>*</c>, a quoted strong entity tag or a comma-separated list of them, or the bare concurrency token.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateUpdateAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        PersistedTeam team,
        UpdateTeamRequest model,
        string? ifMatch,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Authorizes the deletion of the team at the node above the stored team, the parent team for a child team and the
    /// platform for a top-level team, then validates the row of the team to be deleted.
    /// </summary>
    /// <param name="user">The <see cref="ClaimsPrincipal"/> for the current caller.</param>
    /// <param name="controlPlaneTenantId">The identifier of the tenant that serves the management request.</param>
    /// <param name="team">The <see cref="PersistedTeam"/> to be deleted.</param>
    /// <param name="storeManager">The <see cref="IStoreManager"/> for the current unit of work.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ManagementError"/> describing the first failed precondition, or <c>null</c> when valid.</returns>
    ValueTask<ManagementError?> ValidateDeleteAsync(
        ClaimsPrincipal user,
        string controlPlaneTenantId,
        PersistedTeam team,
        IStoreManager storeManager,
        CancellationToken cancellationToken
    );
}
