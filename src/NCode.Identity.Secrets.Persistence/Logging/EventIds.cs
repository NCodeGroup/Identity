// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Secrets.Persistence.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.Secrets.Persistence</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>7000</c>–<c>7999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 7000;

    /// <summary>
    /// A new master key was created and persisted.
    /// </summary>
    public const int MasterKeyCreated = Base + 1;

    /// <summary>
    /// An existing master key was unwrapped by its root key provider.
    /// </summary>
    public const int MasterKeyUnwrapped = Base + 2;

    /// <summary>
    /// Another node won the race to create the master key for an owner, so this node used the stored key.
    /// </summary>
    public const int MasterKeyCreationRaceLost = Base + 3;

    /// <summary>
    /// Master keys are persisted without being wrapped by a recovery key.
    /// </summary>
    public const int RecoveryWrapDisabled = Base + 4;
}
