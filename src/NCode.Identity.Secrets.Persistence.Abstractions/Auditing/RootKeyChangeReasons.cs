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
}
