// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Represents a mediator command that announces a committed change to a team: its row or its settings document was
/// committed. Every such commit changes the team row's concurrency token, which a replica that may have missed the
/// announcement compares with the one it serves. Every registered handler runs. The default handler evicts the team and its tenants from this replica's
/// caches and announces the change on the replica broadcast channel so that every other replica evicts them too. Send it
/// after the change commits; handlers must be idempotent, because one change may be announced more than once.
/// </summary>
/// <param name="TeamId">The identifier of the team that changed.</param>
/// <param name="SettingsChanged"><see langword="true"/> when the team's settings document changed; otherwise,
/// <see langword="false"/>.</param>
// See ADR-1411
[PublicAPI]
public readonly record struct TeamChangedCommand(string TeamId, bool SettingsChanged) : ICommand;
