// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides a default implementation of the <see cref="ITeamSettingsReconciliationTrigger"/> abstraction that writes
/// each request to the <see cref="TeamSettingsReconciliationQueue"/> the <see cref="TeamSettingsReconciliationService"/>
/// reads, where requests coalesce and none is dropped.
/// </summary>
// See ADR-1411 and ADR-1412.
internal sealed class DefaultTeamSettingsReconciliationTrigger(
    TeamSettingsReconciliationQueue queue
) : ITeamSettingsReconciliationTrigger
{
    private TeamSettingsReconciliationQueue Queue { get; } = queue;

    /// <inheritdoc />
    public void Trigger(string? teamId) => Queue.Add(teamId);
}
