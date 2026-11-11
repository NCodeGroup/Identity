// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Persistence.Stores;
using NCode.Identity.Settings;

namespace NCode.Identity.OpenId.Management.Endpoints.Teams;

/// <summary>
/// Checks a change to a team's settings against every client coherence rule in every tenant of the team and of every
/// team beneath it, so a change never leaves a stored client registering a value the changed settings do not permit.
/// </summary>
// See ADR-1426.
internal interface ITeamClientCoherenceChecker
{
    /// <summary>
    /// Finds the client coherence violations that a change of the team's effective settings introduces in every tenant
    /// of the team and of every team beneath it. Each team beneath the team has its settings document resolved onto its
    /// parent's settings before and after the change. A team whose settings document cannot be honored is logged and
    /// skipped together with every team beneath it, because none of its clients is coherent under either settings.
    /// </summary>
    /// <param name="storeManager">The <see cref="IStoreManager"/> used to page through the tenants and their clients.</param>
    /// <param name="teamId">The identifier of the team whose settings change.</param>
    /// <param name="currentTeamSettings">The team's effective settings with its current settings document.</param>
    /// <param name="newTeamSettings">The team's effective settings with its new settings document.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="SettingError"/> instances, keyed by the team setting that does not permit each value, or
    /// an empty list.</returns>
    ValueTask<IReadOnlyList<SettingError>> FindIntroducedViolationsAsync(
        IStoreManager storeManager,
        string teamId,
        IReadOnlySettingCollection currentTeamSettings,
        IReadOnlySettingCollection newTeamSettings,
        CancellationToken cancellationToken
    );
}
