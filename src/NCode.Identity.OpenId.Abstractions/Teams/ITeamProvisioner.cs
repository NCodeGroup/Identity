// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Brings teams into existence on demand, so a tenant a strategy creates always has a team to belong to. It is
/// idempotent: concurrent callers converge on one team.
/// </summary>
// See ADR-1411
[PublicAPI]
public interface ITeamProvisioner
{
    /// <summary>
    /// Ensures the team with the specified identifier exists, creating it with empty settings when it does not.
    /// </summary>
    /// <param name="teamId">The identifier of the team to create.</param>
    /// <param name="displayName">The display name applied when the team is created; an existing team keeps its own.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the existing or newly
    /// created <see cref="PersistedTeam"/>.</returns>
    ValueTask<PersistedTeam> EnsureCreatedAsync(
        string teamId,
        string displayName,
        CancellationToken cancellationToken
    );
}
