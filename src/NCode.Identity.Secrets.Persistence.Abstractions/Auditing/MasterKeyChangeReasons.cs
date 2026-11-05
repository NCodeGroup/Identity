// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.Persistence.Auditing;

/// <summary>
/// Contains the reasons that the audit trail gives for a change to a master key. The set is open.
/// </summary>
// See ADR-1407.
[PublicAPI]
public static class MasterKeyChangeReasons
{
    /// <summary>
    /// An administrator asked for the rekey.
    /// </summary>
    public const string Requested = "requested";

    /// <summary>
    /// The active key outlived the rotation period, so the maintenance pass rotated it.
    /// </summary>
    public const string RotationPeriodElapsed = "rotation_period_elapsed";

    /// <summary>
    /// The retired key passed its grace and no protected value names it, so the maintenance pass destroyed it.
    /// </summary>
    public const string Unreferenced = "unreferenced";
}
