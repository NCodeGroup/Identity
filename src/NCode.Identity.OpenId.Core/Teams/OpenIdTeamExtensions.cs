// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides extension members for <see cref="OpenIdTeam"/>.
/// </summary>
internal static class OpenIdTeamExtensions
{
    extension(OpenIdTeam team)
    {
        /// <summary>
        /// Gets the <see cref="TeamRevision"/> of the team followed by the revision of each team above it, up to the
        /// top-level team.
        /// </summary>
        /// <returns>The revisions of the team and of every team above it, with the team's own revision first.</returns>
        // See ADR-1416 and ADR-1426.
        public ImmutableArray<TeamRevision> GetRevisionLineage()
        {
            var builder = ImmutableArray.CreateBuilder<TeamRevision>();
            builder.Add(new TeamRevision(team.TeamId, team.ConcurrencyToken));

            for (var parent = team.Parent; parent is not null; parent = parent.Parent)
            {
                builder.Add(new TeamRevision(parent.TeamId, parent.ConcurrencyToken));
            }

            return builder.ToImmutable();
        }
    }
}
