// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Settings;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Represents a mediator command that brings everything derived from the server's effective settings in line with
/// them. One replica sends it in the background whenever the server's effective settings differ from the ones they
/// were last reconciled against: after a change to the server's settings document, a configuration reload, or a
/// deployment. Every registered handler runs; the default handlers act on the tenants that the change from
/// <see cref="PreviousSettings"/> affects: they seed a tenant that lacks a key its settings use and report the stored
/// clients of a tenant whose settings a client's rules read and the change altered. A handler is idempotent and
/// continues past an item it cannot handle, because one state may be reconciled more than once.
/// </summary>
/// <param name="PlatformId">The identifier of the server whose effective settings are reconciled.</param>
/// <param name="Settings">The server's effective settings that everything derived from them is brought in line with.</param>
// See ADR-1219 and ADR-1234.
[PublicAPI]
public readonly record struct ReconcilePlatformSettingsCommand(
    string PlatformId,
    IReadOnlySettingCollection Settings
) : ICommand
{
    /// <summary>
    /// Gets the server's effective settings that everything derived from them was last reconciled against, or
    /// <c>null</c> when they are unknown. A handler compares them with <see cref="Settings"/> and skips what the
    /// change cannot affect; with <c>null</c> it reconciles everything.
    /// </summary>
    public IReadOnlySettingCollection? PreviousSettings { get; init; }
}
