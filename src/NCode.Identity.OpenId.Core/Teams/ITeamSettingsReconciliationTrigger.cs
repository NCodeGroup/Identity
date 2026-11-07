// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Asks the <see cref="TeamSettingsReconciliationService"/> to reconcile teams' effective settings in the background.
/// </summary>
// See ADR-1411 and ADR-1412.
internal interface ITeamSettingsReconciliationTrigger
{
    /// <summary>
    /// Requests a reconciliation. Requests that arrive while the service is busy coalesce: every requested team is
    /// reconciled once, and a request for every team covers each team.
    /// </summary>
    /// <param name="teamId">The identifier of the team to reconcile, or <c>null</c> to reconcile every team.</param>
    void Trigger(string? teamId);
}
