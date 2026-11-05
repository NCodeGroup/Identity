// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Persistence;
using NCode.Identity.Secrets.Persistence.Auditing;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logging;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Persistence.Stores;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the default implementation of <see cref="IOwnerRootKeyManager"/>.
/// </summary>
/// <remarks>
/// <para>
/// Setting a key probes it, stores the binding as active, evicts the owner from the ring, tells the observers, and then
/// completes: the owner's master key rotates onto the key unless the active one already sits under it, and every
/// retired master key is rewrapped under it. The earlier material exists in backups under the deployment's root key and
/// its recovery copy, so only a master key created under the owner's key gives the owner sole control of what is
/// written afterwards. Removing the key does not rotate: its material was only ever under the stronger root.
/// </para>
/// <para>
/// The binding is committed before the completion, and the completion is repeated by <see cref="ValidateAsync"/>, so a
/// rotation that is too soon, lost to a race or cut short is finished by the next validation. The observers and the
/// completion of a set or a removal run on a bounded token of their own, never the caller's, so a cancelled caller
/// leaves neither unannounced nor unfinished; a completion that fails is logged and left to the validation.
/// </para>
/// </remarks>
// See ADR-1411.
internal sealed class DefaultOwnerRootKeyManager(
    IKeyWrapperCatalog catalog,
    IStoreManagerFactory storeManagerFactory,
    ISecretProtectorProvider protectorProvider,
    IMasterKeyManager masterKeyManager,
    IMasterKeyRewrapper rewrapper,
    IEnumerable<IOwnerRootKeyChangeObserver> observers,
    IOptions<SecretProtectionOptions> options,
    TimeProvider timeProvider,
    ILogger<DefaultOwnerRootKeyManager> logger
) : IOwnerRootKeyManager
{
    private static readonly MasterKeyRewrapResult NoRewrap = new(0, 0, 0, 0);

    private IKeyWrapperCatalog Catalog { get; } = catalog;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private ISecretProtectorProvider ProtectorProvider { get; } = protectorProvider;

    private IMasterKeyManager MasterKeyManager { get; } = masterKeyManager;

    private IMasterKeyRewrapper Rewrapper { get; } = rewrapper;

    private IReadOnlyList<IOwnerRootKeyChangeObserver> Observers { get; } = [.. observers];

    private IOptions<SecretProtectionOptions> Options { get; } = options;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<DefaultOwnerRootKeyManager> Logger { get; } = logger;

    /// <inheritdoc />
    public IReadOnlyList<string> AllowedKeyWrapperNames =>
        [.. Options.Value.OwnerRootKeys.Providers.Order(StringComparer.Ordinal)];

    /// <inheritdoc />
    public bool TryValidateKeyReference(
        string keyWrapperName,
        string keyReference,
        [NotNullWhen(false)] out string? failure
    )
    {
        ArgumentNullException.ThrowIfNull(keyWrapperName);
        ArgumentNullException.ThrowIfNull(keyReference);

        if (!AllowedKeyWrapperNames.Contains(keyWrapperName, StringComparer.Ordinal))
        {
            failure =
                $"The key wrapper '{keyWrapperName}' is not allowed for the root key of an owner.";
            return false;
        }

        if (!Catalog.TryGetReferenced(keyWrapperName, out var wrapper))
        {
            failure =
                $"The key wrapper '{keyWrapperName}' does not wrap under a key that the owner names.";
            return false;
        }

        if (!wrapper.TryValidateKeyReference(keyReference, out var referenceFailure))
        {
            failure = $"The key reference {referenceFailure}.";
            return false;
        }

        failure = null;
        return true;
    }

    /// <inheritdoc />
    public async ValueTask<OwnerRootKeyInfo?> GetAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var key = Normalize(owner);

        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var binding = await storeManager
            .GetStore<IOwnerRootKeyStore>()
            .FindAsync(key, cancellationToken);

        return binding is null ? null : OwnerRootKeyInfo.From(binding);
    }

    /// <inheritdoc />
    public async ValueTask<OwnerRootKeyChange> SetAsync(
        SecretOwner owner,
        string keyWrapperName,
        string keyReference,
        CancellationToken cancellationToken
    )
    {
        var key = Normalize(owner);

        if (!TryValidateKeyReference(keyWrapperName, keyReference, out var failure))
            throw new ArgumentException(failure, nameof(keyReference));

        var (binding, rows) = await ReadAsync(key, cancellationToken);
        var sameKey =
            binding is not null
            && !IsState(binding, OwnerRootKeyStates.Removing)
            && string.Equals(binding.KeyWrapperName, keyWrapperName, StringComparison.Ordinal)
            && string.Equals(binding.KeyReference, keyReference, StringComparison.Ordinal);

        var now = TimeProvider.GetUtcNow();

        // A different key rotates the master key, so the spacing is checked before anything is written.
        if (
            !sameKey
            && rows.Any(row => row.RetiredWhen is null)
            && MasterKeyRotationSpacing.GetRemaining(
                rows,
                Options.Value.MasterKeys.RefreshInterval,
                now
            )
                is { } remaining
        )
        {
            throw new MasterKeyRotationTooSoonException(key, remaining);
        }

        var probeFailure = await OwnerRootKeyProbe.ProbeAsync(
            Catalog.GetReferenced(keyWrapperName),
            keyReference,
            key,
            cancellationToken
        );
        if (probeFailure is not null)
        {
            Logger.OwnerRootKeyProbeFailed(key, keyWrapperName, keyReference, probeFailure);

            // The key the owner already uses failed, so the owner fails closed as a validation would make it.
            OwnerRootKeyInfo? unavailable = null;
            if (sameKey && binding is not null)
            {
                unavailable = await RecordProbeFailureAsync(
                    key,
                    binding,
                    probeFailure,
                    cancellationToken
                );
            }

            throw new OwnerRootKeyProbeException(
                key,
                keyWrapperName,
                keyReference,
                probeFailure,
                unavailable
            );
        }

        var wasUnavailable = binding is not null && !IsUsable(binding);
        var stored = await StoreBindingAsync(
            key,
            binding,
            keyWrapperName,
            keyReference,
            sameKey,
            now,
            cancellationToken
        );

        Logger.OwnerRootKeySet(key, keyWrapperName, keyReference);
        ProtectorProvider.Evict(key);
        await NotifyAsync(key);

        var change =
            !sameKey ? RootKeyChanges.Set
            : wasUnavailable ? RootKeyChanges.Restored
            : null;

        var (rotation, rewrap) = await CompleteSetAsync(key, stored);

        return new OwnerRootKeyChange(OwnerRootKeyInfo.From(stored), change, rotation, rewrap);
    }

    /// <inheritdoc />
    public async ValueTask<OwnerRootKeyRemoval> RemoveAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var key = Normalize(owner);

        var (binding, _) = await ReadAsync(key, cancellationToken);
        if (binding is null)
            return new OwnerRootKeyRemoval(null, false, NoRewrap);

        var removed = false;
        if (!IsState(binding, OwnerRootKeyStates.Removing))
        {
            binding.State = OwnerRootKeyStates.Removing;
            binding.StateChangedWhen = TimeProvider.GetUtcNow();
            await SaveBindingAsync(binding, cancellationToken);
            removed = true;

            Logger.OwnerRootKeyStateChanged(key, OwnerRootKeyStates.Removing);
            ProtectorProvider.Evict(key);
            await NotifyAsync(key);
        }

        var (remaining, rewrap) = await CompleteRemovalOnItsOwnTokenAsync(key, binding);

        return new OwnerRootKeyRemoval(remaining, removed, rewrap);
    }

    /// <inheritdoc />
    public async ValueTask<OwnerRootKeyValidation> ValidateAsync(
        SecretOwner owner,
        CancellationToken cancellationToken
    )
    {
        var key = Normalize(owner);

        var (binding, _) = await ReadAsync(key, cancellationToken);
        if (binding is null)
            return new OwnerRootKeyValidation(null, null, null, NoRewrap);

        if (IsState(binding, OwnerRootKeyStates.Removing))
        {
            var (remaining, removalRewrap) = await CompleteRemovalAsync(key, cancellationToken);
            return new OwnerRootKeyValidation(remaining, null, null, removalRewrap);
        }

        var probeFailure = await OwnerRootKeyProbe.ProbeAsync(
            Catalog.GetReferenced(binding.KeyWrapperName),
            binding.KeyReference,
            key,
            cancellationToken
        );

        var now = TimeProvider.GetUtcNow();
        var wasUsable = IsUsable(binding);
        string? change = null;
        if (probeFailure is null)
        {
            binding.ValidatedWhen = now;
            binding.Failure = null;
            if (!IsState(binding, OwnerRootKeyStates.Active))
            {
                binding.State = OwnerRootKeyStates.Active;
                binding.StateChangedWhen = now;
                change = RootKeyChanges.Restored;
            }
        }
        else
        {
            Logger.OwnerRootKeyProbeFailed(
                key,
                binding.KeyWrapperName,
                binding.KeyReference,
                probeFailure
            );

            binding.FailedWhen = now;
            binding.Failure = probeFailure;
            if (wasUsable)
            {
                binding.State = OwnerRootKeyStates.Unavailable;
                binding.StateChangedWhen = now;
                change = RootKeyChanges.Unavailable;
            }
            else if (!IsState(binding, OwnerRootKeyStates.Unavailable))
            {
                binding.State = OwnerRootKeyStates.Unavailable;
                binding.StateChangedWhen = now;
            }
        }

        try
        {
            await SaveBindingAsync(binding, cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // Another replica validated or changed the key at the same time, so its result stands.
            var (current, _) = await ReadAsync(key, cancellationToken);
            return new OwnerRootKeyValidation(
                current is null ? null : OwnerRootKeyInfo.From(current),
                null,
                null,
                NoRewrap
            );
        }

        if (change is not null)
        {
            Logger.OwnerRootKeyStateChanged(key, binding.State);
            ProtectorProvider.Evict(key);
            await NotifyAsync(key);
        }

        if (!IsState(binding, OwnerRootKeyStates.Active))
            return new OwnerRootKeyValidation(
                OwnerRootKeyInfo.From(binding),
                change,
                null,
                NoRewrap
            );

        var (rotation, rewrap) = await CompleteAsync(key, binding, cancellationToken);

        return new OwnerRootKeyValidation(OwnerRootKeyInfo.From(binding), change, rotation, rewrap);
    }

    private static SecretOwner Normalize(SecretOwner owner)
    {
        if (owner.OwnerType is null)
        {
            throw new ArgumentException(
                "The owner is the default value of SecretOwner, which identifies no owner.",
                nameof(owner)
            );
        }

        if (
            string.Equals(
                owner.OwnerType,
                SecretOwnerTypes.Server,
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            throw new ArgumentException(
                "The master keys of the server are wrapped by the deployment's root key, so the server cannot bring a root key.",
                nameof(owner)
            );
        }

        return owner.Normalize();
    }

    private static bool IsState(PersistedOwnerRootKey binding, string state) =>
        string.Equals(binding.State, state, StringComparison.Ordinal);

    // A state that the code does not know is not usable, so it fails closed.
    private static bool IsUsable(PersistedOwnerRootKey binding) =>
        IsState(binding, OwnerRootKeyStates.Active);

    // Reads the root key and the master keys of an owner through one unit of work.
    private async ValueTask<(
        PersistedOwnerRootKey? Binding,
        IReadOnlyList<PersistedMasterKey> Rows
    )> ReadAsync(SecretOwner key, CancellationToken cancellationToken)
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var binding = await storeManager
            .GetStore<IOwnerRootKeyStore>()
            .FindAsync(key, cancellationToken);
        var rows = await storeManager.GetStore<IMasterKeyStore>().ListAsync(key, cancellationToken);

        return (binding, rows);
    }

    private async ValueTask SaveBindingAsync(
        PersistedOwnerRootKey binding,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        await storeManager.GetStore<IOwnerRootKeyStore>().UpdateAsync(binding, cancellationToken);
        await storeManager.SaveChangesAsync(cancellationToken);
    }

    // Adds the binding, or updates the existing one with its concurrency token checked.
    private async ValueTask<PersistedOwnerRootKey> StoreBindingAsync(
        SecretOwner key,
        PersistedOwnerRootKey? binding,
        string keyWrapperName,
        string keyReference,
        bool sameKey,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        if (binding is not null)
        {
            binding.KeyWrapperName = keyWrapperName;
            binding.KeyReference = keyReference;
            if (!sameKey)
                binding.CreatedWhen = now;

            if (!IsState(binding, OwnerRootKeyStates.Active))
            {
                binding.State = OwnerRootKeyStates.Active;
                binding.StateChangedWhen = now;
            }

            binding.ValidatedWhen = now;
            binding.Failure = null;

            await SaveBindingAsync(binding, cancellationToken);
            return binding;
        }

        var added = new PersistedOwnerRootKey
        {
            OwnerType = key.OwnerType,
            OwnerId = key.OwnerId,
            KeyWrapperName = keyWrapperName,
            KeyReference = keyReference,
            State = OwnerRootKeyStates.Active,
            CreatedWhen = now,
            StateChangedWhen = now,
            ValidatedWhen = now,
        };

        try
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            await storeManager.GetStore<IOwnerRootKeyStore>().AddAsync(added, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The unit of work that failed to save is broken, so the winner is read through a new one.
            await using var readManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var winner = await readManager
                .GetStore<IOwnerRootKeyStore>()
                .FindAsync(key, cancellationToken);
            if (winner is null)
                throw;

            throw new StoreConcurrencyException(
                "Another writer set the owner's root key first.",
                exception
            );
        }

        return added;
    }

    // Records that the key the owner already uses failed a probe, as a validation would, without hiding the failure that
    // the caller reports. Returns the stored key when the failure made it unavailable.
    private async ValueTask<OwnerRootKeyInfo?> RecordProbeFailureAsync(
        SecretOwner key,
        PersistedOwnerRootKey binding,
        string failure,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var changed = IsUsable(binding);
        binding.FailedWhen = now;
        binding.Failure = failure;
        if (!IsState(binding, OwnerRootKeyStates.Unavailable))
        {
            binding.State = OwnerRootKeyStates.Unavailable;
            binding.StateChangedWhen = now;
        }

        try
        {
            await SaveBindingAsync(binding, cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // Another writer changed the binding at the same time, so its state stands.
            return null;
        }

        if (!changed)
            return null;

        Logger.OwnerRootKeyStateChanged(key, binding.State);
        ProtectorProvider.Evict(key);
        await NotifyAsync(key);

        return OwnerRootKeyInfo.From(binding);
    }

    // Rotates the owner's master key onto its root key when the active one does not sit under it, and conforms every
    // other master key to it.
    private async ValueTask<(
        MasterKeyRotation? Rotation,
        MasterKeyRewrapResult Rewrap
    )> CompleteAsync(
        SecretOwner key,
        PersistedOwnerRootKey binding,
        CancellationToken cancellationToken
    )
    {
        var (_, rows) = await ReadAsync(key, cancellationToken);
        var active = rows.SingleOrDefault(row => row.RetiredWhen is null);

        MasterKeyRotation? rotation = null;
        if (active is null || !MasterKeyFactory.Conforms(active, binding))
        {
            try
            {
                rotation = await MasterKeyManager.RotateAsync(key, cancellationToken);
            }
            catch (Exception exception)
                when (exception is MasterKeyRotationTooSoonException or StoreConcurrencyException)
            {
                // The validation service completes the rotation on its next pass.
                Logger.OwnerRootKeyRotationDeferred(exception, key);
            }
        }

        var rewrap = await Rewrapper.RewrapAsync(
            new MasterKeyRewrapRequest { Owner = key, NonconformingOnly = true },
            cancellationToken
        );

        return (rotation, rewrap);
    }

    // Moves every master key under the deployment's root key and deletes the binding once none remains under the key.
    // The binding is read again after the move, because the store renews its concurrency token with every save.
    private async ValueTask<(
        OwnerRootKeyInfo? Remaining,
        MasterKeyRewrapResult Rewrap
    )> CompleteRemovalAsync(SecretOwner key, CancellationToken cancellationToken)
    {
        var rewrap = await Rewrapper.RewrapAsync(
            new MasterKeyRewrapRequest { Owner = key, NonconformingOnly = true },
            cancellationToken
        );

        var (binding, rows) = await ReadAsync(key, cancellationToken);
        if (binding is null)
            return (null, rewrap);

        // The owner set a key again during the removal, so the binding is no longer being removed and stays.
        if (!IsState(binding, OwnerRootKeyStates.Removing))
            return (OwnerRootKeyInfo.From(binding), rewrap);

        // A master key written or rewrapped under the owner's key after the rewrap listed it stays under that key, so
        // the binding stays until none remains.
        var remaining = Math.Max(
            rewrap.Failed,
            rows.Count(row => row.RootKeyReference is not null)
        );
        if (remaining > 0)
        {
            binding.FailedWhen = TimeProvider.GetUtcNow();
            binding.Failure = $"{remaining} master keys remain under the owner root key";
            Logger.OwnerRootKeyRemovalIncomplete(key, remaining);

            try
            {
                await SaveBindingAsync(binding, cancellationToken);
            }
            catch (StoreConcurrencyException)
            {
                // Another replica recorded its attempt at the same time.
            }

            return (OwnerRootKeyInfo.From(binding), rewrap);
        }

        try
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            await storeManager
                .GetStore<IOwnerRootKeyStore>()
                .RemoveAsync(binding, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (StoreConcurrencyException)
        {
            // The binding changed during the removal, so it is reported as it is and the next pass looks again.
            var (current, _) = await ReadAsync(key, cancellationToken);
            return (current is null ? null : OwnerRootKeyInfo.From(current), rewrap);
        }

        Logger.OwnerRootKeyRemoved(key);
        ProtectorProvider.Evict(key);
        await NotifyAsync(key);

        return (null, rewrap);
    }

    // Completes a committed set on a bounded token of its own, so that the caller's cancellation cannot cut it short. A
    // completion that fails leaves the committed binding as it is, and the validation service finishes it.
    private async ValueTask<(
        MasterKeyRotation? Rotation,
        MasterKeyRewrapResult Rewrap
    )> CompleteSetAsync(SecretOwner key, PersistedOwnerRootKey binding)
    {
        (MasterKeyRotation? Rotation, MasterKeyRewrapResult Rewrap) completion = (null, NoRewrap);
        try
        {
            await PostCommitWork.RunAsync(
                TimeProvider,
                async (state, cancellationToken) =>
                    completion = await CompleteAsync(state.Key, state.Binding, cancellationToken),
                (Key: key, Binding: binding)
            );
        }
        catch (Exception exception)
        {
            Logger.OwnerRootKeyCompletionDeferred(exception, key);
            return (null, NoRewrap);
        }

        return completion;
    }

    // Completes a committed removal on a bounded token of its own, so that the caller's cancellation cannot cut it
    // short. A completion that fails leaves the committed binding as it is, and the validation service finishes it.
    private async ValueTask<(
        OwnerRootKeyInfo? Remaining,
        MasterKeyRewrapResult Rewrap
    )> CompleteRemovalOnItsOwnTokenAsync(SecretOwner key, PersistedOwnerRootKey binding)
    {
        (OwnerRootKeyInfo? Remaining, MasterKeyRewrapResult Rewrap) completion = (
            OwnerRootKeyInfo.From(binding),
            NoRewrap
        );
        try
        {
            await PostCommitWork.RunAsync(
                TimeProvider,
                async (state, cancellationToken) =>
                    completion = await CompleteRemovalAsync(state, cancellationToken),
                key
            );
        }
        catch (Exception exception)
        {
            Logger.OwnerRootKeyCompletionDeferred(exception, key);
            return (OwnerRootKeyInfo.From(binding), NoRewrap);
        }

        return completion;
    }

    // Calls every observer on a bounded token of its own and swallows each failure, since the change is committed.
    private async ValueTask NotifyAsync(SecretOwner key)
    {
        foreach (var observer in Observers)
        {
            try
            {
                await PostCommitWork.RunAsync(
                    TimeProvider,
                    static (state, cancellationToken) =>
                        state.Observer.ChangedAsync(state.Key, cancellationToken),
                    (Observer: observer, Key: key)
                );
            }
            catch (Exception exception)
            {
                Logger.OwnerRootKeyObserverFailed(exception, key);
            }
        }
    }
}
