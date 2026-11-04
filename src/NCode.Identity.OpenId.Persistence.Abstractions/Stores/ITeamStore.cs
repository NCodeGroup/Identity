// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Persistence.Stores;

/// <summary>
/// Provides an abstraction for a store which persists <see cref="PersistedTeam"/> instances.
/// </summary>
// See ADR-1403
[PublicAPI]
public interface ITeamStore : IStore<PersistedTeam>
{
    /// <summary>
    /// Gets a single page of <see cref="PersistedTeam"/> instances ordered by their identifier, using keyset
    /// (cursor) pagination.
    /// </summary>
    /// <param name="page">The <see cref="PageRequest"/> describing the pagination window (cursor and limit).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page of
    /// <see cref="PersistedTeam"/> instances and the cursor for the next page.</returns>
    ValueTask<PagedResult<PersistedTeam>> GetPageAsync(
        PageRequest page,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get a <see cref="PersistedTeam"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="teamId">The identifier of the <see cref="PersistedTeam"/> instance to retrieve.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedTeam"/> instance matching the specified identifier if it exists.</returns>
    ValueTask<PersistedTeam?> GetOrDefaultAsync(string teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the <see cref="PersistedTeamSettings"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="teamId">The identifier of the OpenId Team.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the <see cref="PersistedTeamSettings"/> for the specified identifier.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no team has the specified identifier.</exception>
    ValueTask<PersistedTeamSettings> GetSettingsAsync(
        string teamId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to get the <see cref="PersistedTeamSettings"/> instance from the store with the specified identifier.
    /// </summary>
    /// <param name="teamId">The identifier of the OpenId Team.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the
    /// <see cref="PersistedTeamSettings"/> if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedTeamSettings?> GetSettingsOrDefaultAsync(
        string teamId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the JSON settings for an OpenId Team in the store.
    /// </summary>
    /// <param name="persistedTeamSettings">The <see cref="PersistedTeamSettings"/> instance to update.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateSettingsAsync(
        PersistedTeamSettings persistedTeamSettings,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Updates the effective settings, and their fingerprint, that the team's tenants and clients were last reconciled
    /// against.
    /// </summary>
    /// <param name="persistedTeam">The <see cref="PersistedTeam"/> instance carrying the reconciled settings.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask UpdateReconciledSettingsAsync(
        PersistedTeam persistedTeam,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Removes an OpenId Team from the store. The removal is rejected while any tenant still belongs to the team.
    /// Removing a team retires its identifier, so it is never reissued.
    /// </summary>
    /// <param name="teamId">The identifier of the OpenId Team to remove.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// if the team was removed; otherwise <c>false</c> when no matching team existed.</returns>
    /// <exception cref="InvalidOperationException">Thrown when a tenant still belongs to the team.</exception>
    ValueTask<bool> RemoveAsync(string teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether an OpenId Team still has dependents: any tenant that belongs to it.
    /// </summary>
    /// <param name="teamId">The identifier of the OpenId Team.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c>
    /// when any tenant belongs to the team; otherwise <c>false</c> (including when no such team exists).</returns>
    ValueTask<bool> HasDependentsAsync(string teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Determines whether the specified team identifier belonged to a deleted team. A team identifier is never
    /// reissued.
    /// </summary>
    /// <param name="teamId">The team identifier to look up.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when a
    /// deleted team had the identifier; otherwise <c>false</c>.</returns>
    ValueTask<bool> IsTeamIdRetiredAsync(string teamId, CancellationToken cancellationToken);
}
