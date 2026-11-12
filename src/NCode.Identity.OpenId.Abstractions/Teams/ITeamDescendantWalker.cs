// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Persistence.DataContracts;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Walks the teams beneath a team, every level, each once and each parent before its children, holding at most one page
/// of child teams per level, so the memory a walk uses is bounded however many teams the subtree holds.
/// </summary>
// See ADR-1426.
[PublicAPI]
public interface ITeamDescendantWalker
{
    /// <summary>
    /// Visits every team beneath the specified team, each parent before its children. Nothing is visited when the team
    /// has no child teams or does not exist.
    /// </summary>
    /// <param name="teamId">The identifier of the team whose descendants are visited; the team itself is not visited.</param>
    /// <param name="visitAsync">The callback invoked for each descendant. It returns <see langword="true"/> to walk the
    /// teams beneath the visited team and <see langword="false"/> to skip them.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask WalkDescendantsAsync(
        string teamId,
        Func<PersistedTeam, CancellationToken, ValueTask<bool>> visitAsync,
        CancellationToken cancellationToken
    );
}
