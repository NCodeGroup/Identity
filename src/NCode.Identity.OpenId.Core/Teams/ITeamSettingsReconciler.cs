// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Brings everything derived from a team's effective settings in line with them whenever those settings differ from
/// the ones they were last reconciled against.
/// </summary>
// See ADR-1219 and ADR-1403.
internal interface ITeamSettingsReconciler
{
    /// <summary>
    /// Reconciles the team's effective settings when they differ from the ones recorded as reconciled, with one
    /// replica per change.
    /// </summary>
    /// <param name="teamId">The identifier of the team whose effective settings are reconciled.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The result is
    /// <see langword="true"/> when the team's effective settings are reconciled, whether already or by this call, or
    /// when no such team exists, and <see langword="false"/> when the caller should try again later because another
    /// replica holds the lease or the record lost a race with a change to the team.
    /// </returns>
    ValueTask<bool> ReconcileAsync(string teamId, CancellationToken cancellationToken);
}
