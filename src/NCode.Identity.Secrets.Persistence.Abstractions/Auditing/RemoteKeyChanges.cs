// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Contains the names of the changes to a remote key that the audit trail records. The set is open: a package that
/// changes remote keys in another way names its own change.
/// </summary>
// See ADR-1478.
[PublicAPI]
public static class RemoteKeyChanges
{
    /// <summary>
    /// The key was destroyed in the key management service that held it.
    /// </summary>
    public const string Destroyed = "destroyed";
}
