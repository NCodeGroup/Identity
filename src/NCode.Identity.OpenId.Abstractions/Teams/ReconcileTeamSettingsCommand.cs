// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Represents a mediator command that brings everything derived from a team's effective settings in line with them.
/// One replica sends it in the background whenever the team's effective settings differ from the ones they were last
/// reconciled against: after a change to the team's or the platform's settings document, a configuration reload, or a
/// deployment. Every registered handler runs; the default handlers act on the team's tenants that the change from
/// <see cref="PreviousSettings"/> affects: they seed a tenant that lacks a key its settings use and report the stored
/// clients of a tenant whose settings a client's rules read and the change altered. A handler is idempotent and
/// continues past an item it cannot handle, because one state may be reconciled more than once.
/// </summary>
/// <param name="TeamId">The identifier of the team whose effective settings are reconciled.</param>
/// <param name="Settings">The team's effective settings, the parent of each of its tenants, that everything derived
/// from them is brought in line with.</param>
// See ADR-1412 and ADR-1411.
[PublicAPI]
public readonly record struct ReconcileTeamSettingsCommand(
    string TeamId,
    IReadOnlySettingCollection Settings
) : ICommand
{
    /// <summary>
    /// Gets the team's effective settings that everything derived from them was last reconciled against, or
    /// <c>null</c> when they are unknown. A handler compares them with <see cref="Settings"/> and skips what the
    /// change cannot affect; with <c>null</c> it reconciles everything.
    /// </summary>
    public IReadOnlySettingCollection? PreviousSettings { get; init; }
}
