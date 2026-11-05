// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Contains the names of the changes to an owner root key that the audit trail records. The set is open.
/// </summary>
// See ADR-1411.
[PublicAPI]
public static class RootKeyChanges
{
    /// <summary>
    /// The owner set or changed its root key.
    /// </summary>
    public const string Set = "set";

    /// <summary>
    /// The owner's root key started to be removed.
    /// </summary>
    public const string Removed = "removed";

    /// <summary>
    /// The root key failed a probe, so the owner fails closed.
    /// </summary>
    public const string Unavailable = "unavailable";

    /// <summary>
    /// The root key passed a probe after it failed one, so the owner is served again.
    /// </summary>
    public const string Restored = "restored";
}
