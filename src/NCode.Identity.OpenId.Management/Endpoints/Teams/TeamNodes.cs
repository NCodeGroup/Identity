// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Management.Authorization;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

/// <summary>
/// Names the authorization nodes that sit above a team.
/// </summary>
// See ADR-1418 and ADR-1426.
internal static class TeamNodes
{
    /// <summary>
    /// Gets the node above a team: its parent team, or the platform for a top-level team.
    /// </summary>
    /// <param name="controlPlaneTenantId">The identifier of the control plane tenant that owns the nodes.</param>
    /// <param name="parentTeamId">The identifier of the team's parent team, or <c>null</c> for a top-level team.</param>
    /// <returns>The parent team's node when <paramref name="parentTeamId"/> is not <c>null</c>; otherwise the platform node.</returns>
    internal static ResourceNode GetNodeAbove(string controlPlaneTenantId, string? parentTeamId) =>
        parentTeamId is null
            ? ResourceNode.For(
                controlPlaneTenantId,
                ResourceNodeTypes.Platform,
                OpenIdConstants.PlatformResourceId
            )
            : ResourceNode.For(controlPlaneTenantId, ResourceNodeTypes.Team, parentTeamId);
}
