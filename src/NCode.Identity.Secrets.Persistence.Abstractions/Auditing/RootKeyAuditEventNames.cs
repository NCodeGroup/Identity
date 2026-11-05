// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Contains the names of the audit events that record the changes to an owner root key. They follow the catalog's
/// <c>&lt;resource&gt;.&lt;change&gt;</c> convention.
/// </summary>
// See ADR-1414.
[PublicAPI]
public static class RootKeyAuditEventNames
{
    /// <summary>
    /// The prefix of every root key event; the change (<see cref="RootKeyChanges"/>) follows it.
    /// </summary>
    public const string Prefix = "root_key";

    /// <summary>
    /// The name of the event that records setting or changing the root key.
    /// </summary>
    public const string Set = $"{Prefix}.{RootKeyChanges.Set}";

    /// <summary>
    /// The name of the event that records the start of removing the root key.
    /// </summary>
    public const string Removed = $"{Prefix}.{RootKeyChanges.Removed}";

    /// <summary>
    /// The name of the event that records a root key becoming unavailable.
    /// </summary>
    public const string Unavailable = $"{Prefix}.{RootKeyChanges.Unavailable}";

    /// <summary>
    /// The name of the event that records a root key being restored.
    /// </summary>
    public const string Restored = $"{Prefix}.{RootKeyChanges.Restored}";

    /// <summary>
    /// Gets the name of the event that records the specified change, such as <c>root_key.unavailable</c>.
    /// </summary>
    /// <param name="change">The change: one of the values in <see cref="RootKeyChanges"/>.</param>
    /// <returns>The event name.</returns>
    public static string For(string change) => $"{Prefix}.{change}";
}
