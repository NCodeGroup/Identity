// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Contains the names of the changes to a master key that the audit trail records. The set is open: a package that
/// changes master keys in another way names its own change.
/// </summary>
// See ADR-1407.
[PublicAPI]
public static class MasterKeyChanges
{
    /// <summary>
    /// The master key was retired and its owner received a new active version.
    /// </summary>
    public const string Rotated = "rotated";

    /// <summary>
    /// The retired master key was destroyed.
    /// </summary>
    public const string Destroyed = "destroyed";
}
