// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Contains the reasons that the audit trail gives for a change to a remote key. The set is open.
/// </summary>
// See ADR-1478.
[PublicAPI]
public static class RemoteKeyChangeReasons
{
    /// <summary>
    /// No stored secret names the key, and it is older than the sweep's grace period.
    /// </summary>
    public const string Orphaned = "orphaned";
}
