// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Contains the reconciliations that a <see cref="TeamSettingsReconciliationService"/> pass takes from the
/// <see cref="TeamSettingsReconciliationQueue"/>.
/// </summary>
/// <param name="EveryTeam">
/// <see langword="true"/> to reconcile every team; otherwise, <see langword="false"/> to reconcile only the teams in
/// <paramref name="TeamIds"/>.
/// </param>
/// <param name="TeamIds">
/// The identifiers of the teams to reconcile, each once. The list is empty when <paramref name="EveryTeam"/> is
/// <see langword="true"/>.
/// </param>
internal readonly record struct TeamSettingsReconciliationRequest(
    bool EveryTeam,
    IReadOnlyList<string> TeamIds
);
