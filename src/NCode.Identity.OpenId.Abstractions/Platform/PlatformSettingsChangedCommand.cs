// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using JetBrains.Annotations;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Represents a mediator command that announces a committed change to a server's persisted settings document,
/// carrying the document before and after the change. Every registered handler runs. The default handler reads the
/// document again on this replica and announces the change to every replica, so each serves it at once; the
/// reconciliation of the server's settings then seeds the tenants and checks the clients the change affects. Send it
/// after the change commits; handlers must be idempotent, because one change may be announced more than once.
/// </summary>
/// <param name="ServerId">The identifier of the server whose settings changed.</param>
/// <param name="PreviousSettings">The server's persisted settings document before the change.</param>
/// <param name="Settings">The server's persisted settings document after the change.</param>
// See ADR-1169 and ADR-1219.
[PublicAPI]
public readonly record struct ServerSettingsChangedCommand(
    string ServerId,
    JsonElement PreviousSettings,
    JsonElement Settings
) : ICommand;
