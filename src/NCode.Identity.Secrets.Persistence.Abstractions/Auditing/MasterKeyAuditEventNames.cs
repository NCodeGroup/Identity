// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Contains the names of the audit events that record the changes to a master key. They follow the catalog's
/// <c>&lt;resource&gt;.&lt;change&gt;</c> convention.
/// </summary>
// See ADR-1407.
[PublicAPI]
public static class MasterKeyAuditEventNames
{
    /// <summary>
    /// The prefix of every master key event; the change (<see cref="MasterKeyChanges"/>) follows it.
    /// </summary>
    public const string Prefix = "master_key";

    /// <summary>
    /// The name of the event that records a rotation.
    /// </summary>
    public const string Rotated = $"{Prefix}.{MasterKeyChanges.Rotated}";

    /// <summary>
    /// The name of the event that records the destruction of a retired key.
    /// </summary>
    public const string Destroyed = $"{Prefix}.{MasterKeyChanges.Destroyed}";

    /// <summary>
    /// Gets the name of the event that records the specified change, such as <c>master_key.rotated</c>.
    /// </summary>
    /// <param name="change">The change: one of the values in <see cref="MasterKeyChanges"/>.</param>
    /// <returns>The event name.</returns>
    public static string For(string change) => $"{Prefix}.{change}";
}
