// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logging;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// Provides the default implementation of <see cref="ISecretProtectorProvider"/>, which keeps the unwrapped master
/// keys of every owner in memory, loads them from the <see cref="IMasterKeyStore"/> on demand, and creates the first
/// master key of an owner when the owner has none.
/// </summary>
/// <remarks>
/// <para>
/// Owners are matched without regard to case. The loading of the master keys of one owner is serialized, so
/// concurrent callers for the same owner unwrap each master key once, while different owners load in parallel. When
/// several nodes create the first master key of the same owner at the same time, the node whose key is stored first
/// wins, and the other nodes use the stored key and discard their own.
/// </para>
/// <para>
/// A cached entry is used for at most <see cref="MasterKeyOptions.RefreshInterval"/>. After that the next call reads
/// the active row and the rows of every cached key again, reuses the key material it already holds, and drops the keys
/// whose rows no longer exist, so a replica that missed a rotation or a destruction catches up without a broadcast.
/// </para>
/// <para>
/// An owner that brings a root key is served only while the key works. Every
/// <see cref="OwnerRootKeyOptions.ValidationInterval"/> a cached entry that holds master keys under the owner's root
/// key unwraps the active master key and one master key of every other root key reference through the key provider, so
/// a key that the owner revoked or destroyed stops this replica within that interval; the other master keys of a
/// reference that unwrapped keep the material the entry holds. A load that fails for such an owner removes the entry
/// and refuses the owner for <see cref="OwnerRootKeyOptions.RetryInterval"/> with an
/// <see cref="OwnerRootKeyUnavailableException"/>, without calling the store or the key. Every failure of an
/// owner-rooted load fails the owner closed for that interval, because the ring cannot tell an outage from a
/// revocation and must never serve material it could not validate again.
/// </para>
/// <para>
/// The ring owns the unwrapped master keys that it caches and zeroes them when it is disposed. Evicting an owner does
/// not zero its master keys because a protector that was handed out earlier may still be using them. An owner that is
/// evicted while its master keys load is not cached.
/// </para>
/// </remarks>
/// <param name="factory">The factory that creates new master keys and unwraps persisted master keys.</param>
/// <param name="storeManagerFactory">The factory that creates the unit of work that reads and writes master keys.</param>
/// <param name="recovery">The wrapper that reports whether master keys are wrapped under a recovery key.</param>
/// <param name="options">The options whose master key refresh interval bounds how long an entry is used.</param>
/// <param name="timeProvider">The clock that ages the cached entries.</param>
/// <param name="logger">The logger.</param>
// See ADR-1235 and ADR-1414.
internal class DefaultMasterKeyRing(
    MasterKeyFactory factory,
    IStoreManagerFactory storeManagerFactory,
    IRecoveryKeyWrapper recovery,
    IOptions<SecretProtectionOptions> options,
    TimeProvider timeProvider,
    ILogger<DefaultMasterKeyRing> logger
) : ISecretProtectorProvider, IDisposable
{
    private int _recoveryWarningLogged;

    // Counts the evictions so that a load can tell whether an eviction ran while it was reading the master keys.
    private long _evictionCount;

    private MasterKeyFactory Factory { get; } = factory;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private IRecoveryKeyWrapper Recovery { get; } = recovery;

    private IOptions<SecretProtectionOptions> Options { get; } = options;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<DefaultMasterKeyRing> Logger { get; } = logger;

    private ConcurrentDictionary<SecretOwner, MasterKeyRingEntry> Entries { get; } =
        new(SecretOwnerComparer.Instance);

    private ConcurrentDictionary<SecretOwner, SemaphoreSlim> Gates { get; } =
        new(SecretOwnerComparer.Instance);

    // The timestamp of the last failed load of each owner that brings a root key.
    private ConcurrentDictionary<SecretOwner, long> Failures { get; } =
        new(SecretOwnerComparer.Instance);

    /// <inheritdoc />
    public async ValueTask<ISecretProtector> GetProtectorAsync(
        SecretOwner owner,
        IReadOnlyCollection<string> protectedValues,
        CancellationToken cancellationToken
    )
    {
        ThrowIfDefault(owner);
        ArgumentNullException.ThrowIfNull(protectedValues);

        LogRecoveryWarningOnce();

        var requiredKeyIds = GetRequiredKeyIds(protectedValues);
        var refreshInterval = Options.Value.MasterKeys.RefreshInterval;
        var rootKeys = Options.Value.OwnerRootKeys;

        // An owner whose root key failed is refused until its retry interval passes, without a store or key call.
        ThrowIfRefused(owner, rootKeys.RetryInterval);

        // The dictionaries ignore case, so a cached owner is found without normalizing the owner.
        if (
            Entries.TryGetValue(owner, out var cached)
            && !cached.IsStale(TimeProvider, refreshInterval)
            && !cached.NeedsValidation(TimeProvider, rootKeys.ValidationInterval)
            && cached.Contains(requiredKeyIds)
        )
            return CreateProtector(owner, cached);

        var key = owner.Normalize();
        var gate = Gates.GetOrAdd(key, static _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            // An eviction does not take the gate, so the count is read before the cache is, and an eviction that runs
            // from here on changes it.
            var evictionsBefore = Volatile.Read(ref _evictionCount);

            // Another caller may have loaded the master keys while this caller waited for the gate.
            ThrowIfRefused(owner, rootKeys.RetryInterval);
            if (
                Entries.TryGetValue(key, out cached)
                && !cached.IsStale(TimeProvider, refreshInterval)
                && !cached.NeedsValidation(TimeProvider, rootKeys.ValidationInterval)
                && cached.Contains(requiredKeyIds)
            )
                return CreateProtector(owner, cached);

            MasterKeyRingEntry entry;
            try
            {
                entry = await LoadEntryAsync(
                    key,
                    cached,
                    requiredKeyIds,
                    rootKeys.ValidationInterval,
                    cancellationToken
                );
            }
            catch (OwnerRootKeyUnavailableException exception)
            {
                // The load reports every failure of an owner-rooted load this way. Whatever the failure was, the
                // owner cannot be served through its root key: it fails closed, and the cached material of the owner
                // is dropped. A refusal is recorded only when no eviction ran during the load, which is the test that
                // publishes an entry.
                Entries.TryRemove(key, out _);
                if (Volatile.Read(ref _evictionCount) == evictionsBefore)
                    Failures[key] = TimeProvider.GetTimestamp();

                Logger.OwnerRootKeyLoadFailed(exception.InnerException ?? exception, key);
                throw;
            }

            Failures.TryRemove(key, out _);
            Entries[key] = entry;

            // An eviction that ran during the load removed nothing that this load published, so the entry is retracted
            // to leave the evicted owner uncached. The protector still uses the keys, which an eviction does not zero.
            // An eviction of another owner also retracts the entry, which only costs the next call one reload.
            if (Volatile.Read(ref _evictionCount) != evictionsBefore)
                Entries.TryRemove(new KeyValuePair<SecretOwner, MasterKeyRingEntry>(key, entry));

            // A protected value that names a master key the owner has no row for fails only this call. It is not a
            // failure of the load, so the entry stays cached and no refusal is recorded for an owner-rooted owner.
            ThrowIfMissing(key, entry.Keys, requiredKeyIds);

            return CreateProtector(owner, entry);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public bool TryReadMasterKeyId(ReadOnlySpan<char> protectedValue, out Guid masterKeyId) =>
        SecretProtectionFormat.TryReadMasterKeyId(protectedValue, out masterKeyId);

    /// <inheritdoc />
    public string GetMasterKeyIdPrefix(Guid masterKeyId) =>
        SecretProtectionFormat.GetMasterKeyIdPrefix(masterKeyId);

    /// <inheritdoc />
    public void Evict(SecretOwner owner)
    {
        ThrowIfDefault(owner);

        // The count changes before the entry is removed, so a load that publishes afterwards sees the change.
        Interlocked.Increment(ref _evictionCount);
        Entries.TryRemove(owner, out _);
        Failures.TryRemove(owner, out _);
    }

    /// <inheritdoc />
    public void EvictAll()
    {
        // The count changes before the entries are removed, so a load that publishes afterwards sees the change.
        Interlocked.Increment(ref _evictionCount);
        Entries.Clear();
        Failures.Clear();
    }

    /// <summary>
    /// Zeroes every cached master key and releases the locks of the owners.
    /// </summary>
    public void Dispose()
    {
        foreach (var entry in Entries.Values)
        {
            foreach (var masterKey in entry.Keys.Values)
            {
                masterKey.Dispose();
            }
        }

        Entries.Clear();

        foreach (var gate in Gates.Values)
        {
            gate.Dispose();
        }

        Gates.Clear();
    }

    // The constructor of SecretOwner validates both parts, but default(SecretOwner) bypasses it.
    private static void ThrowIfDefault(SecretOwner owner)
    {
        if (owner.OwnerType is null)
        {
            throw new ArgumentException(
                "The owner is the default value of SecretOwner, which identifies no owner; create it with SecretOwner.Platform, SecretOwner.ForTenant or its constructor.",
                nameof(owner)
            );
        }
    }

    // Determines whether a failure of a load that involves an owner root key fails the owner closed. A caller's
    // cancellation does not, and neither does a failure that already does.
    private static bool FailsClosed(
        bool isOwnerRooted,
        Exception exception,
        CancellationToken cancellationToken
    ) =>
        isOwnerRooted
        && exception is not OwnerRootKeyUnavailableException
        && !(exception is OperationCanceledException && cancellationToken.IsCancellationRequested);

    private void ThrowIfRefused(SecretOwner owner, TimeSpan retryInterval)
    {
        if (Failures.IsEmpty || !Failures.TryGetValue(owner, out var failedTimestamp))
            return;

        if (TimeProvider.GetElapsedTime(failedTimestamp) >= retryInterval)
            return;

        Logger.OwnerRootKeyRefused(owner);
        throw new OwnerRootKeyUnavailableException(owner);
    }

    private static DefaultSecretProtector CreateProtector(
        SecretOwner owner,
        MasterKeyRingEntry entry
    ) => new(owner, entry.Active, entry.Keys);

    private void LogRecoveryWarningOnce()
    {
        if (Recovery.IsConfigured)
            return;

        if (Interlocked.CompareExchange(ref _recoveryWarningLogged, 1, 0) == 0)
            Logger.RecoveryWrapDisabled();
    }

    private static IReadOnlySet<Guid> GetRequiredKeyIds(IReadOnlyCollection<string> protectedValues)
    {
        // A call that reads no secret needs no master key, and shares one empty set instead of allocating its own.
        if (protectedValues.Count == 0)
            return FrozenSet<Guid>.Empty;

        var keyIds = new HashSet<Guid>(protectedValues.Count);

        foreach (var protectedValue in protectedValues)
        {
            // A value that is not an envelope is ignored here and fails when it is unprotected.
            if (SecretProtectionFormat.TryReadMasterKeyId(protectedValue, out var keyId))
                keyIds.Add(keyId);
        }

        return keyIds;
    }

    private async ValueTask<MasterKeyRingEntry> LoadEntryAsync(
        SecretOwner key,
        MasterKeyRingEntry? cached,
        IReadOnlySet<Guid> requiredKeyIds,
        TimeSpan validationInterval,
        CancellationToken cancellationToken
    )
    {
        // The cached master keys are read again by their identifiers. Their material is reused, so only the rows that
        // are not cached are unwrapped, unless the entry holds material under an owner root key that is due for
        // validation: then the active master key and one master key of every root key reference are unwrapped again,
        // and the material of the other keys under a reference that unwrapped is reused.
        var cachedKeys = cached?.Keys ?? FrozenDictionary<Guid, MasterKey>.Empty;
        var validating =
            cached is not null && cached.NeedsValidation(TimeProvider, validationInterval);

        // A failure of a load through an owner root key fails the owner closed, which is known from the cached entry
        // until the rows are read.
        var isOwnerRooted = cached?.IsOwnerRooted ?? false;

        // The master keys that this load created or unwrapped, which are zeroed again if the load fails. At most the
        // active master key and each required master key are unwrapped.
        var loaded = new List<MasterKey>(requiredKeyIds.Count + 1);

        try
        {
            var (activeRow, createdKey, rows, rowsOwnerRooted) = await ReadRowsAsync(
                key,
                cachedKeys,
                requiredKeyIds,
                isOwnerRooted,
                cancellationToken
            );

            isOwnerRooted |= rowsOwnerRooted;

            if (createdKey is not null)
                loaded.Add(createdKey);

            // A cached key whose row is gone was destroyed, so it is not carried into the new entry. A cached key is
            // gone when its identifier is neither the active row's nor one of the rows that were read.
            var rowKeyIds = new HashSet<Guid>(rows.Count);
            foreach (var row in rows)
            {
                rowKeyIds.Add(row.KeyId);
            }

            foreach (var cachedKeyId in cachedKeys.Keys)
            {
                if (cachedKeyId != activeRow.KeyId && !rowKeyIds.Contains(cachedKeyId))
                    Logger.MasterKeyDropped(key, cachedKeyId);
            }

            // The root key references and access identities whose root key unwrapped a master key of this load.
            var unwrappedReferences = validating
                ? new HashSet<(string Reference, string? AccessIdentity)>()
                : null;

            var keys = new Dictionary<Guid, MasterKey>(rows.Count + 1);
            var reused = false;

            var active = createdKey;
            if (active is null)
            {
                if (!validating && cachedKeys.TryGetValue(activeRow.KeyId, out active))
                {
                    reused = true;
                }
                else
                {
                    active = await UnwrapRowAsync(key, activeRow, loaded, cancellationToken);

                    if (activeRow.RootKeyReference is not null)
                        unwrappedReferences?.Add(
                            (activeRow.RootKeyReference, activeRow.RootKeyAccessIdentity)
                        );
                }
            }

            keys[activeRow.KeyId] = active;

            // A validation unwraps one row of each root key reference and access identity that the active row does not
            // cover.
            if (unwrappedReferences is not null)
            {
                foreach (var row in rows)
                {
                    if (
                        row.RootKeyReference is not null
                        && !keys.ContainsKey(row.KeyId)
                        && unwrappedReferences.Add(
                            (row.RootKeyReference, row.RootKeyAccessIdentity)
                        )
                    )
                        keys[row.KeyId] = await UnwrapRowAsync(key, row, loaded, cancellationToken);
                }
            }

            // The remaining rows reuse the cached material, which is what a validation does for the rows under a
            // reference and access identity that unwrapped and for the rows under the deployment's root key. A row
            // without cached material is unwrapped.
            foreach (var row in rows)
            {
                if (keys.ContainsKey(row.KeyId))
                    continue;

                if (cachedKeys.TryGetValue(row.KeyId, out var cachedKey))
                {
                    keys[row.KeyId] = cachedKey;
                    reused = true;
                }
                else
                {
                    keys[row.KeyId] = await UnwrapRowAsync(key, row, loaded, cancellationToken);
                }
            }

            var now = TimeProvider.GetTimestamp();

            // A validation has just unwrapped the entry through its root keys. Otherwise reused material keeps the
            // moment it was unwrapped, so it is validated on schedule however often the entry is refreshed.
            var unwrappedTimestamp =
                !validating && reused && cached is not null ? cached.UnwrappedTimestamp : now;

            return new MasterKeyRingEntry(
                active,
                keys.ToFrozenDictionary(),
                now,
                rowsOwnerRooted,
                unwrappedTimestamp
            );
        }
        catch (Exception exception)
        {
            foreach (var masterKey in loaded)
            {
                masterKey.Dispose();
            }

            if (FailsClosed(isOwnerRooted, exception, cancellationToken))
                throw new OwnerRootKeyUnavailableException(key, exception);

            throw;
        }
    }

    // Unwraps a persisted master key with its root key provider and records it so that a failed load zeroes it.
    private async ValueTask<MasterKey> UnwrapRowAsync(
        SecretOwner key,
        PersistedMasterKey row,
        List<MasterKey> loaded,
        CancellationToken cancellationToken
    )
    {
        var masterKey = await Factory.UnwrapAsync(row, cancellationToken);
        loaded.Add(masterKey);

        Logger.MasterKeyUnwrapped(key, row.KeyId, row.RootKeyProvider);

        return masterKey;
    }

    private static void ThrowIfMissing(
        SecretOwner key,
        FrozenDictionary<Guid, MasterKey> keys,
        IReadOnlySet<Guid> requiredKeyIds
    )
    {
        foreach (var keyId in requiredKeyIds)
        {
            if (!keys.ContainsKey(keyId))
                throw new CryptographicException(
                    $"The master key '{keyId}' of '{key}' does not exist."
                );
        }
    }

    // Reads the active row and the required rows that are not cached through one store manager, and creates the
    // active row if the owner has none. The result reports whether the load involves an owner root key, which makes a
    // failure of the load a failure of that key.
    private async ValueTask<(
        PersistedMasterKey ActiveRow,
        MasterKey? CreatedKey,
        IReadOnlyList<PersistedMasterKey> Rows,
        bool IsOwnerRooted
    )> ReadRowsAsync(
        SecretOwner key,
        FrozenDictionary<Guid, MasterKey> cachedKeys,
        IReadOnlySet<Guid> requiredKeyIds,
        bool cachedIsOwnerRooted,
        CancellationToken cancellationToken
    )
    {
        var isOwnerRooted = false;
        try
        {
            await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
            var store = storeManager.GetStore<IMasterKeyStore>();

            // Both reads happen before any write, and the active row's identifier is not queried a second time.
            var activeRow = await store.FindActiveAsync(key, cancellationToken);
            var activeKeyId = activeRow?.KeyId;

            // Every cached key is read again to learn whether it still exists, together with each required key that is
            // not cached. The identifiers are collected in a set, so the list holds no duplicates; it stays empty when
            // the active key is the only one involved.
            HashSet<Guid>? queryKeyIds = null;
            foreach (var keyId in cachedKeys.Keys)
            {
                if (keyId == activeKeyId)
                    continue;

                queryKeyIds ??= new HashSet<Guid>(cachedKeys.Count + requiredKeyIds.Count);
                queryKeyIds.Add(keyId);
            }

            foreach (var keyId in requiredKeyIds)
            {
                if (keyId == activeKeyId)
                    continue;

                queryKeyIds ??= new HashSet<Guid>(requiredKeyIds.Count);
                queryKeyIds.Add(keyId);
            }

            IReadOnlyList<PersistedMasterKey> rows = queryKeyIds is null
                ? []
                : await store.FindByKeyIdsAsync(key, queryKeyIds, cancellationToken);

            // A row under an owner root key makes this load a load through that key.
            var rowsUnderOwnerRootKey =
                activeRow?.RootKeyReference is not null
                || rows.Any(row => row.RootKeyReference is not null);
            if (rowsUnderOwnerRootKey)
                isOwnerRooted = true;

            // The owner's root key matters when a key is created, and when a row that is read sits under it.
            PersistedOwnerRootKey? rootKey = null;
            if (activeRow is null || rowsUnderOwnerRootKey)
            {
                rootKey = await storeManager
                    .GetStore<IOwnerRootKeyStore>()
                    .FindAsync(key, cancellationToken);

                if (MasterKeyFactory.IsOwnerRooted(rootKey))
                    isOwnerRooted = true;

                // A key that failed its last probe, or whose state is unknown, fails the owner closed.
                if (
                    MasterKeyFactory.IsOwnerRooted(rootKey)
                    && !string.Equals(
                        rootKey.State,
                        OwnerRootKeyStates.Active,
                        StringComparison.Ordinal
                    )
                )
                    throw new OwnerRootKeyUnavailableException(key);
            }

            if (activeRow is not null)
                return (activeRow, null, rows, isOwnerRooted);

            var (createdRow, createdKey) = await CreateActiveRowAsync(
                key,
                storeManager,
                store,
                rootKey,
                cancellationToken
            );

            if (createdRow.RootKeyReference is not null)
                isOwnerRooted = true;

            return (createdRow, createdKey, rows, isOwnerRooted);
        }
        catch (Exception exception)
            when (FailsClosed(cachedIsOwnerRooted || isOwnerRooted, exception, cancellationToken))
        {
            throw new OwnerRootKeyUnavailableException(key, exception);
        }
    }

    // Stores a new active master key for the owner, or returns the active row that another node stored first.
    private async ValueTask<(PersistedMasterKey Row, MasterKey? CreatedKey)> CreateActiveRowAsync(
        SecretOwner key,
        IStoreManager storeManager,
        IMasterKeyStore store,
        PersistedOwnerRootKey? rootKey,
        CancellationToken cancellationToken
    )
    {
        var version = await store.GetLatestVersionAsync(key, cancellationToken) + 1;
        var created = await Factory.CreateAsync(key, version, rootKey, cancellationToken);
        try
        {
            await store.AddAsync(created.Row, cancellationToken);
            await storeManager.SaveChangesAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            created.Key.Dispose();
            throw;
        }
        catch (Exception)
        {
            created.Key.Dispose();

            // The unit of work that failed to save is broken, so the winner is read through a new one.
            var winner = await FindActiveRowInNewStoreManagerAsync(key, cancellationToken);
            if (winner is null)
                throw;

            Logger.MasterKeyCreationRaceLost(key);

            return (winner, null);
        }

        Logger.MasterKeyCreated(
            key,
            created.Row.KeyId,
            created.Row.Version,
            created.Row.RootKeyProvider
        );

        return (created.Row, created.Key);
    }

    // Reads the active row of the owner through a fresh store manager.
    private async ValueTask<PersistedMasterKey?> FindActiveRowInNewStoreManagerAsync(
        SecretOwner key,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IMasterKeyStore>();

        return await store.FindActiveAsync(key, cancellationToken);
    }
}
