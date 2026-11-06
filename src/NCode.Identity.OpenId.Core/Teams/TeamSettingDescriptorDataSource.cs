// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Collections.Providers;
using NCode.Identity.OpenId.Persistence;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides an additive <see cref="SettingDescriptor"/> data source for the team settings: <c>max_tenants</c>, the
/// most tenants a team may hold; <c>max_teams</c>, the most teams that may sit beneath a team; and
/// <c>max_team_depth</c>, the deepest level a team is created at. None is advertised in discovery.
/// </summary>
// See ADR-1411.
// See ADR-1418.
// See ADR-1426.
internal class TeamSettingDescriptorDataSource(INullChangeToken nullChangeToken)
    : ICollectionDataSource<SettingDescriptor>
{
    private INullChangeToken NullChangeToken { get; } = nullChangeToken;

    /// <inheritdoc />
    public IChangeToken GetChangeToken() => NullChangeToken;

    /// <inheritdoc />
    public IEnumerable<SettingDescriptor> Collection
    {
        get
        {
            // max_tenants: the most tenants a team may hold. Unset means unlimited. A limit: the platform's
            // administrators set it on a team, and Min keeps the platform's ceiling.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.MaxTenants,
                AppliesTo = [OpenIdSettingScopes.Team],
                Rule = SettingRules.AtLeast(0),

                IsDiscoverable = false,
                IsLimit = true,
                OnMerge = SettingMerge.Min,
            };

            // max_teams: the most teams that may sit beneath a team, at every level. Unset means unlimited. A limit:
            // the administrators above a team set it, and Min keeps every ceiling above it.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.MaxTeams,
                AppliesTo = [OpenIdSettingScopes.Team],
                Rule = SettingRules.AtLeast(0),

                IsDiscoverable = false,
                IsLimit = true,
                OnMerge = SettingMerge.Min,
            };

            // max_team_depth: the deepest level a team is created at, a top-level team being at level 1. Unset means
            // TeamPaths.MaxDepth. Platform scope, so it is not a limit and has no merge.
            yield return new SettingDescriptor<int>
            {
                Name = OpenIdSettingNames.MaxTeamDepth,
                AppliesTo = [OpenIdSettingScopes.Platform],
                Rule = SettingRules.Between(1, TeamPaths.MaxDepth),

                IsDiscoverable = false,
            };
        }
    }
}
