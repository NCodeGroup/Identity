// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedRoleAssignment"/> instances: grants of a
/// role to a principal at a node of the resource hierarchy (resource-scoped role assignments, including ownership).
/// Assignments are immutable records; to change one, remove it and add a replacement.
/// </summary>
[PublicAPI]
public interface IRoleAssignmentStore : IStore
{
    /// <summary>
    /// Adds a new role assignment to the store.
    /// </summary>
    /// <param name="assignment">The <see cref="PersistedRoleAssignment"/> to add.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask AddAsync(PersistedRoleAssignment assignment, CancellationToken cancellationToken);

    /// <summary>
    /// Attempts to get a role assignment from the store by its opaque identifier.
    /// </summary>
    /// <param name="assignmentId">The opaque identifier of the role assignment.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedRoleAssignment"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedRoleAssignment?> GetOrDefaultAsync(
        string assignmentId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes a role assignment from the store by its opaque identifier.
    /// </summary>
    /// <param name="assignmentId">The opaque identifier of the role assignment to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask RemoveAsync(string assignmentId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets every role assignment granted at the specified resource node (used by owner-list management and the
    /// one-owner invariant).
    /// </summary>
    /// <param name="resourceType">The type of the resource node.</param>
    /// <param name="resourceId">The identifier of the resource node.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the assignments at
    /// the node.</returns>
    ValueTask<IReadOnlyList<PersistedRoleAssignment>> GetByResourceAsync(
        string resourceType,
        string resourceId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets every role assignment granted to the specified principal (used by the authorization decision, which then
    /// filters to the resource's ancestor nodes).
    /// </summary>
    /// <param name="principalId">The identifier of the principal.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the principal's
    /// assignments.</returns>
    ValueTask<IReadOnlyList<PersistedRoleAssignment>> GetByPrincipalAsync(
        string principalId,
        CancellationToken cancellationToken
    );
}
