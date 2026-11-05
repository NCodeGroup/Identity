// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Secrets.Persistence.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.Secrets.Persistence</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    /// <summary>
    /// Logs that a new master key was created and persisted.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="keyId">The identifier of the master key.</param>
    /// <param name="version">The version of the master key.</param>
    /// <param name="rootKeyProvider">The name of the root key provider that wrapped the master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyCreated,
        Level = LogLevel.Information,
        Message = "Created master key {KeyId} (version {Version}) for '{Owner}' wrapped by the root key provider '{RootKeyProvider}'"
    )]
    internal static partial void MasterKeyCreated(
        this ILogger logger,
        SecretOwner owner,
        Guid keyId,
        int version,
        string rootKeyProvider
    );

    /// <summary>
    /// Logs that an existing master key was unwrapped by its root key provider.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="keyId">The identifier of the master key.</param>
    /// <param name="rootKeyProvider">The name of the root key provider that unwrapped the master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyUnwrapped,
        Level = LogLevel.Debug,
        Message = "Unwrapped master key {KeyId} for '{Owner}' with the root key provider '{RootKeyProvider}'"
    )]
    internal static partial void MasterKeyUnwrapped(
        this ILogger logger,
        SecretOwner owner,
        Guid keyId,
        string rootKeyProvider
    );

    /// <summary>
    /// Logs that another node created the master key for an owner first, so the stored key is used instead.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner of the master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyCreationRaceLost,
        Level = LogLevel.Debug,
        Message = "Another node created the master key for '{Owner}' first; using the stored master key"
    )]
    internal static partial void MasterKeyCreationRaceLost(this ILogger logger, SecretOwner owner);

    /// <summary>
    /// Logs that master keys are persisted without being wrapped by a recovery key.
    /// </summary>
    /// <param name="logger">The logger.</param>
    [LoggerMessage(
        EventId = EventIds.RecoveryWrapDisabled,
        Level = LogLevel.Warning,
        Message = "Master keys are not wrapped by a recovery key; losing the root key loses every stored secret."
    )]
    internal static partial void RecoveryWrapDisabled(this ILogger logger);

    /// <summary>
    /// Logs that a cached master key was dropped because its row no longer exists.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="keyId">The identifier of the master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyDropped,
        Level = LogLevel.Information,
        Message = "Dropped master key {KeyId} of '{Owner}' from memory because it no longer exists"
    )]
    internal static partial void MasterKeyDropped(
        this ILogger logger,
        SecretOwner owner,
        Guid keyId
    );

    /// <summary>
    /// Logs that a master key rotated to a new version.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="keyId">The identifier of the new active master key.</param>
    /// <param name="version">The version of the new active master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyRotated,
        Level = LogLevel.Information,
        Message = "Rotated the master key of '{Owner}' to {KeyId} (version {Version})"
    )]
    internal static partial void MasterKeyRotated(
        this ILogger logger,
        SecretOwner owner,
        Guid keyId,
        int version
    );

    /// <summary>
    /// Logs that the values protected by retired master keys were rewrapped under the active master key.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner of the master keys.</param>
    /// <param name="rewrapped">The number of values that were rewrapped.</param>
    /// <param name="failed">The number of values that could not be rewrapped.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeysRewrapped,
        Level = LogLevel.Information,
        Message = "Rewrapped {Rewrapped} value(s) of '{Owner}' under the active master key; {Failed} failed"
    )]
    internal static partial void MasterKeysRewrapped(
        this ILogger logger,
        SecretOwner owner,
        int rewrapped,
        int failed
    );

    /// <summary>
    /// Logs that a retired master key was destroyed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="keyId">The identifier of the destroyed master key.</param>
    /// <param name="version">The version of the destroyed master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyDestroyed,
        Level = LogLevel.Information,
        Message = "Destroyed master key {KeyId} (version {Version}) of '{Owner}'"
    )]
    internal static partial void MasterKeyDestroyed(
        this ILogger logger,
        SecretOwner owner,
        Guid keyId,
        int version
    );

    /// <summary>
    /// Logs that an observer of master key changes failed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="owner">The owner whose master key changed.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyObserverFailed,
        Level = LogLevel.Warning,
        Message = "An observer of the master key change of '{Owner}' failed"
    )]
    internal static partial void MasterKeyObserverFailed(
        this ILogger logger,
        Exception exception,
        SecretOwner owner
    );

    /// <summary>
    /// Logs that a rewrap of persisted master keys completed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="examined">The number of master keys examined.</param>
    /// <param name="rewrapped">The number of master keys rewrapped.</param>
    /// <param name="verified">The number of master keys verified without a write.</param>
    /// <param name="failed">The number of master keys that failed.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyRewrapCompleted,
        Level = LogLevel.Information,
        Message = "Master key rewrap completed: {Examined} examined, {Rewrapped} rewrapped, {Verified} verified, {Failed} failed"
    )]
    internal static partial void MasterKeyRewrapCompleted(
        this ILogger logger,
        int examined,
        int rewrapped,
        int verified,
        int failed
    );

    /// <summary>
    /// Logs that a persisted master key could not be rewrapped.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="keyId">The identifier of the master key.</param>
    /// <param name="version">The version of the master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyRewrapRowFailed,
        Level = LogLevel.Warning,
        Message = "Master key {KeyId} (version {Version}) of '{Owner}' could not be rewrapped"
    )]
    internal static partial void MasterKeyRewrapRowFailed(
        this ILogger logger,
        Exception exception,
        SecretOwner owner,
        Guid keyId,
        int version
    );

    /// <summary>
    /// Logs that values still name a retired master key after a rewrap.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner of the master keys.</param>
    /// <param name="failed">The number of values that could not be rewrapped.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyRewrapIncomplete,
        Level = LogLevel.Warning,
        Message = "{Failed} value(s) of '{Owner}' could not be rewrapped and keep their retired master key"
    )]
    internal static partial void MasterKeyRewrapIncomplete(
        this ILogger logger,
        SecretOwner owner,
        int failed
    );

    /// <summary>
    /// Logs that a rotation adopted the successor that another replica stored first.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="keyId">The identifier of the adopted active master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyRotationAdoptedSuccessor,
        Level = LogLevel.Information,
        Message = "Another replica stored the successor {KeyId} of the master key of '{Owner}' first; the rotation adopts it"
    )]
    internal static partial void MasterKeyRotationAdoptedSuccessor(
        this ILogger logger,
        SecretOwner owner,
        Guid keyId
    );

    /// <summary>
    /// Logs that a rotation whose successor could not be stored re-activated the retired master key.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The exception that stopped the successor from being stored.</param>
    /// <param name="owner">The owner of the master key.</param>
    /// <param name="keyId">The identifier of the re-activated master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyRotationReverted,
        Level = LogLevel.Warning,
        Message = "The successor of the master key of '{Owner}' could not be stored; the master key {KeyId} is active again"
    )]
    internal static partial void MasterKeyRotationReverted(
        this ILogger logger,
        Exception exception,
        SecretOwner owner,
        Guid keyId
    );

    /// <summary>
    /// Logs that a rotation whose successor could not be stored could not re-activate the retired master key.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The exception that stopped the retired key from being re-activated.</param>
    /// <param name="owner">The owner of the master key.</param>
    [LoggerMessage(
        EventId = EventIds.MasterKeyRotationRevertFailed,
        Level = LogLevel.Error,
        Message = "The retired master key of '{Owner}' could not be re-activated; the next protector of the owner creates its key"
    )]
    internal static partial void MasterKeyRotationRevertFailed(
        this ILogger logger,
        Exception exception,
        SecretOwner owner
    );

    /// <summary>
    /// Logs that the master keys of an owner that brings a root key could not be loaded.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="owner">The owner whose root key failed.</param>
    [LoggerMessage(
        EventId = EventIds.OwnerRootKeyLoadFailed,
        Level = LogLevel.Error,
        Message = "The master keys of '{Owner}' could not be loaded through the root key it brings; the owner fails closed"
    )]
    internal static partial void OwnerRootKeyLoadFailed(
        this ILogger logger,
        Exception exception,
        SecretOwner owner
    );

    /// <summary>
    /// Logs that a request for the master keys of an owner is refused because its root key failed recently.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner whose root key failed.</param>
    [LoggerMessage(
        EventId = EventIds.OwnerRootKeyRefused,
        Level = LogLevel.Debug,
        Message = "The master keys of '{Owner}' are refused until its root key is tried again"
    )]
    internal static partial void OwnerRootKeyRefused(this ILogger logger, SecretOwner owner);

    /// <summary>
    /// Logs that an owner set or changed its root key.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner.</param>
    /// <param name="keyWrapperName">The name of the key wrapper.</param>
    /// <param name="keyReference">The reference to the key.</param>
    [LoggerMessage(
        EventId = EventIds.OwnerRootKeySet,
        Level = LogLevel.Information,
        Message = "Owner '{Owner}' set its root key to '{KeyReference}' of key wrapper '{KeyWrapperName}'"
    )]
    internal static partial void OwnerRootKeySet(
        this ILogger logger,
        SecretOwner owner,
        string keyWrapperName,
        string keyReference
    );

    /// <summary>
    /// Logs that a probe of an owner root key failed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner.</param>
    /// <param name="keyWrapperName">The name of the key wrapper.</param>
    /// <param name="keyReference">The reference to the key.</param>
    /// <param name="failure">The description of the failure.</param>
    [LoggerMessage(
        EventId = EventIds.OwnerRootKeyProbeFailed,
        Level = LogLevel.Warning,
        Message = "The root key '{KeyReference}' of key wrapper '{KeyWrapperName}' of owner '{Owner}' failed its probe: {Failure}"
    )]
    internal static partial void OwnerRootKeyProbeFailed(
        this ILogger logger,
        SecretOwner owner,
        string keyWrapperName,
        string keyReference,
        string failure
    );

    /// <summary>
    /// Logs that the state of an owner root key changed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner.</param>
    /// <param name="state">The new state.</param>
    [LoggerMessage(
        EventId = EventIds.OwnerRootKeyStateChanged,
        Level = LogLevel.Warning,
        Message = "The root key of owner '{Owner}' is {State}"
    )]
    internal static partial void OwnerRootKeyStateChanged(
        this ILogger logger,
        SecretOwner owner,
        string state
    );

    /// <summary>
    /// Logs that removing an owner root key left master keys under it.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner.</param>
    /// <param name="remaining">The number of master keys that remain under the owner's root key.</param>
    [LoggerMessage(
        EventId = EventIds.OwnerRootKeyRemovalIncomplete,
        Level = LogLevel.Warning,
        Message = "{Remaining} master keys of owner '{Owner}' remain under its root key; the removal is retried"
    )]
    internal static partial void OwnerRootKeyRemovalIncomplete(
        this ILogger logger,
        SecretOwner owner,
        int remaining
    );

    /// <summary>
    /// Logs that an owner root key is removed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="owner">The owner.</param>
    [LoggerMessage(
        EventId = EventIds.OwnerRootKeyRemoved,
        Level = LogLevel.Information,
        Message = "The root key of owner '{Owner}' is removed and its master keys are under the deployment's root key"
    )]
    internal static partial void OwnerRootKeyRemoved(this ILogger logger, SecretOwner owner);

    /// <summary>
    /// Logs that the rotation onto an owner root key is left to the validation service.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The reason the rotation is deferred.</param>
    /// <param name="owner">The owner.</param>
    [LoggerMessage(
        EventId = EventIds.OwnerRootKeyRotationDeferred,
        Level = LogLevel.Warning,
        Message = "The rotation of owner '{Owner}' onto its root key is deferred to the validation service"
    )]
    internal static partial void OwnerRootKeyRotationDeferred(
        this ILogger logger,
        Exception exception,
        SecretOwner owner
    );

    /// <summary>
    /// Logs that an observer of owner root key changes failed.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The failure.</param>
    /// <param name="owner">The owner whose root key changed.</param>
    [LoggerMessage(
        EventId = EventIds.OwnerRootKeyObserverFailed,
        Level = LogLevel.Warning,
        Message = "An observer of the root key change of '{Owner}' failed"
    )]
    internal static partial void OwnerRootKeyObserverFailed(
        this ILogger logger,
        Exception exception,
        SecretOwner owner
    );

    /// <summary>
    /// Logs that the completion of a committed owner root key change is left to the validation service.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="exception">The reason the completion is deferred.</param>
    /// <param name="owner">The owner whose root key changed.</param>
    [LoggerMessage(
        EventId = EventIds.OwnerRootKeyCompletionDeferred,
        Level = LogLevel.Warning,
        Message = "The completion of the root key change of owner '{Owner}' is deferred to the validation service"
    )]
    internal static partial void OwnerRootKeyCompletionDeferred(
        this ILogger logger,
        Exception exception,
        SecretOwner owner
    );
}
