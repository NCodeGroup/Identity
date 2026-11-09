// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Contains the names of the audit events that record the changes to a remote key. They follow the catalog's
/// <c>&lt;resource&gt;.&lt;change&gt;</c> convention.
/// </summary>
// See ADR-1478.
[PublicAPI]
public static class RemoteKeyAuditEventNames
{
    /// <summary>
    /// The prefix of every remote key event; the change (<see cref="RemoteKeyChanges"/>) follows it.
    /// </summary>
    public const string Prefix = "remote_key";

    /// <summary>
    /// The name of the event that records the destruction of a key.
    /// </summary>
    public const string Destroyed = $"{Prefix}.{RemoteKeyChanges.Destroyed}";

    /// <summary>
    /// Gets the name of the event that records the specified change, such as <c>remote_key.destroyed</c>.
    /// </summary>
    /// <param name="change">The change: one of the values in <see cref="RemoteKeyChanges"/>.</param>
    /// <returns>The event name.</returns>
    public static string For(string change) => $"{Prefix}.{change}";
}
