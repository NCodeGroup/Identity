// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Asks the <see cref="TeamSettingsReconciliationService"/> to reconcile teams' effective settings in the background.
/// </summary>
// See ADR-1219 and ADR-1403.
internal interface ITeamSettingsReconciliationTrigger
{
    /// <summary>
    /// Requests a reconciliation. Requests that arrive while the service is busy queue up to a bound, and the oldest is
    /// dropped beyond it.
    /// </summary>
    /// <param name="teamId">The identifier of the team to reconcile, or <c>null</c> to reconcile every team.</param>
    void Trigger(string? teamId);
}
