// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Contains the reasons that the audit trail gives for a change to an owner root key. The set is open.
/// </summary>
// See ADR-1411.
[PublicAPI]
public static class RootKeyChangeReasons
{
    /// <summary>
    /// An administrator asked for the change.
    /// </summary>
    public const string Requested = "requested";

    /// <summary>
    /// A probe of the key failed.
    /// </summary>
    public const string ValidationFailed = "validation_failed";

    /// <summary>
    /// A probe of the key succeeded after an earlier one failed.
    /// </summary>
    public const string ValidationSucceeded = "validation_succeeded";

    /// <summary>
    /// Gets the reason that a probe of the key gives for a change.
    /// </summary>
    /// <param name="change">The change: one of the values in <see cref="RootKeyChanges"/>.</param>
    /// <returns>
    /// <see cref="ValidationFailed"/> when the change is <see cref="RootKeyChanges.Unavailable"/>; otherwise
    /// <see cref="ValidationSucceeded"/>.
    /// </returns>
    public static string ForValidation(string change) =>
        string.Equals(change, RootKeyChanges.Unavailable, StringComparison.Ordinal)
            ? ValidationFailed
            : ValidationSucceeded;
}
