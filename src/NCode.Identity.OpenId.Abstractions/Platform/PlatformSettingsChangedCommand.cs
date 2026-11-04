// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Platform;

/// <summary>
/// Represents a mediator command that announces a committed change to a platform's persisted settings document,
/// carrying the document before and after the change. Every registered handler runs. The default handler reads the
/// document again on this replica and announces the change to every replica, so each serves it at once; the
/// reconciliation of the platform's settings then seeds the tenants and checks the clients the change affects. Send it
/// after the change commits; handlers must be idempotent, because one change may be announced more than once.
/// </summary>
/// <param name="PreviousSettings">The platform's persisted settings document before the change.</param>
/// <param name="Settings">The platform's persisted settings document after the change.</param>
// See ADR-1169 and ADR-1219.
[PublicAPI]
public readonly record struct PlatformSettingsChangedCommand(
    JsonElement PreviousSettings,
    JsonElement Settings
) : ICommand;
