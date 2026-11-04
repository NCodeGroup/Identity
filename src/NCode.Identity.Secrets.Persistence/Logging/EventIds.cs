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

    /// <summary>
    /// A cached master key was dropped because its row no longer exists.
    /// </summary>
    public const int MasterKeyDropped = Base + 5;

    /// <summary>
    /// A master key rotated to a new version.
    /// </summary>
    public const int MasterKeyRotated = Base + 6;

    /// <summary>
    /// The values that retired master keys protect were rewrapped under the active master key.
    /// </summary>
    public const int MasterKeysRewrapped = Base + 7;

    /// <summary>
    /// A retired master key was destroyed.
    /// </summary>
    public const int MasterKeyDestroyed = Base + 8;

    /// <summary>
    /// An observer of master key changes failed.
    /// </summary>
    public const int MasterKeyObserverFailed = Base + 9;

    /// <summary>
    /// A rewrap of persisted master keys completed.
    /// </summary>
    public const int MasterKeyRewrapCompleted = Base + 10;

    /// <summary>
    /// A persisted master key could not be rewrapped.
    /// </summary>
    public const int MasterKeyRewrapRowFailed = Base + 11;

    /// <summary>
    /// A rewrap of the values under retired master keys left values that still name a retired key.
    /// </summary>
    public const int MasterKeyRewrapIncomplete = Base + 12;

    /// <summary>
    /// A rotation adopted the successor that another replica stored first.
    /// </summary>
    public const int MasterKeyRotationAdoptedSuccessor = Base + 13;

    /// <summary>
    /// A rotation whose successor could not be stored re-activated the retired master key.
    /// </summary>
    public const int MasterKeyRotationReverted = Base + 14;

    /// <summary>
    /// A rotation whose successor could not be stored could not re-activate the retired master key.
    /// </summary>
    public const int MasterKeyRotationRevertFailed = Base + 15;
}
