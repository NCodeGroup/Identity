#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logging;
using NCode.Persistence.Stores;

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
/// The ring owns the unwrapped master keys that it caches and zeroes them when it is disposed. Evicting an owner does
/// not zero its master keys because a protector that was handed out earlier may still be using them. An owner that is
/// evicted while its master keys load is not cached.
/// </para>
/// </remarks>
/// <param name="factory">The factory that creates new master keys and unwraps persisted master keys.</param>
/// <param name="storeManagerFactory">The factory that creates the unit of work that reads and writes master keys.</param>
/// <param name="recovery">The wrapper that reports whether master keys are wrapped under a recovery key.</param>
/// <param name="logger">The logger.</param>
// See ADR-0235.
internal class DefaultMasterKeyRing(
    MasterKeyFactory factory,
    IStoreManagerFactory storeManagerFactory,
    IRecoveryKeyWrapper recovery,
    ILogger<DefaultMasterKeyRing> logger
) : ISecretProtectorProvider, IDisposable
{
    private int _recoveryWarningLogged;

    // Counts the evictions so that a load can tell whether an eviction ran while it was reading the master keys.
    private long _evictionCount;

    private MasterKeyFactory Factory { get; } = factory;

    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;

    private IRecoveryKeyWrapper Recovery { get; } = recovery;

    private ILogger<DefaultMasterKeyRing> Logger { get; } = logger;

    private ConcurrentDictionary<SecretOwner, MasterKeyRingEntry> Entries { get; } =
        new(SecretOwnerComparer.Instance);

    private ConcurrentDictionary<SecretOwner, SemaphoreSlim> Gates { get; } =
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

        // The dictionaries ignore case, so a cached owner is found without normalizing the owner.
        if (Entries.TryGetValue(owner, out var cached) && cached.Contains(requiredKeyIds))
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
            if (Entries.TryGetValue(key, out cached) && cached.Contains(requiredKeyIds))
                return CreateProtector(owner, cached);

            var entry = await LoadEntryAsync(key, cached, requiredKeyIds, cancellationToken);
            Entries[key] = entry;

            // An eviction that ran during the load removed nothing that this load published, so the entry is retracted
            // to leave the evicted owner uncached. The protector still uses the keys, which an eviction does not zero.
            // An eviction of another owner also retracts the entry, which only costs the next call one reload.
            if (Volatile.Read(ref _evictionCount) != evictionsBefore)
                Entries.TryRemove(new KeyValuePair<SecretOwner, MasterKeyRingEntry>(key, entry));

            return CreateProtector(owner, entry);
        }
        finally
        {
            gate.Release();
        }
    }

    /// <inheritdoc />
    public void Evict(SecretOwner owner)
    {
        ThrowIfDefault(owner);

        // The count changes before the entry is removed, so a load that publishes afterwards sees the change.
        Interlocked.Increment(ref _evictionCount);
        Entries.TryRemove(owner, out _);
    }

    /// <inheritdoc />
    public void EvictAll()
    {
        // The count changes before the entries are removed, so a load that publishes afterwards sees the change.
        Interlocked.Increment(ref _evictionCount);
        Entries.Clear();
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
                "The owner is the default value of SecretOwner, which identifies no owner; create it with SecretOwner.ForServer, SecretOwner.ForTenant or its constructor.",
                nameof(owner)
            );
        }
    }

    private static DefaultSecretProtector CreateProtector(
        SecretOwner owner,
        MasterKeyRingEntry entry
    ) => new(owner, entry.Active, entry.Keys);

    private void LogRecoveryWarningOnce()
    {
        if (Recovery.RecoveryKeyId is not null)
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
        CancellationToken cancellationToken
    )
    {
        // The cached master keys are reused by their identifiers, so only the rows that are not cached are unwrapped.
        // The dictionary has room for the cached keys, each required key and the active key.
        var keys = new Dictionary<Guid, MasterKey>(
            (cached?.Keys.Count ?? 0) + requiredKeyIds.Count + 1
        );

        if (cached is not null)
        {
            foreach (var pair in cached.Keys)
            {
                keys[pair.Key] = pair.Value;
            }
        }

        // The master keys that this load created or unwrapped, which are zeroed again if the load fails. At most the
        // active master key and each required master key are unwrapped.
        var loaded = new List<MasterKey>(requiredKeyIds.Count + 1);

        try
        {
            var (activeRow, createdKey, rows) = await ReadRowsAsync(
                key,
                keys,
                requiredKeyIds,
                cancellationToken
            );

            if (createdKey is not null)
                loaded.Add(createdKey);

            var active = createdKey;
            if (active is null && !keys.TryGetValue(activeRow.KeyId, out active))
                active = await UnwrapRowAsync(key, activeRow, loaded, cancellationToken);

            keys[activeRow.KeyId] = active;

            foreach (var row in rows)
            {
                if (!keys.ContainsKey(row.KeyId))
                    keys[row.KeyId] = await UnwrapRowAsync(key, row, loaded, cancellationToken);
            }

            ThrowIfMissing(key, keys, requiredKeyIds);

            return new MasterKeyRingEntry(active, keys.ToFrozenDictionary());
        }
        catch
        {
            foreach (var masterKey in loaded)
            {
                masterKey.Dispose();
            }

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
        Dictionary<Guid, MasterKey> keys,
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
    // active row if the owner has none.
    private async ValueTask<(
        PersistedMasterKey ActiveRow,
        MasterKey? CreatedKey,
        IReadOnlyList<PersistedMasterKey> Rows
    )> ReadRowsAsync(
        SecretOwner key,
        Dictionary<Guid, MasterKey> keys,
        IReadOnlySet<Guid> requiredKeyIds,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IMasterKeyStore>();

        // Both reads happen before any write, and the active row's identifier is not queried a second time.
        var activeRow = await store.FindActiveAsync(key, cancellationToken);
        var activeKeyId = activeRow?.KeyId;

        // The required identifiers are a set, so the list of missing ones holds no duplicates; it stays null when
        // every required master key is already cached or active.
        List<Guid>? missingKeyIds = null;
        foreach (var keyId in requiredKeyIds)
        {
            if (keys.ContainsKey(keyId) || keyId == activeKeyId)
                continue;

            missingKeyIds ??= new List<Guid>(requiredKeyIds.Count);
            missingKeyIds.Add(keyId);
        }

        IReadOnlyList<PersistedMasterKey> rows = missingKeyIds is null
            ? []
            : await store.FindByKeyIdsAsync(key, missingKeyIds, cancellationToken);

        if (activeRow is not null)
            return (activeRow, null, rows);

        var (createdRow, createdKey) = await CreateActiveRowAsync(
            key,
            storeManager,
            store,
            cancellationToken
        );

        return (createdRow, createdKey, rows);
    }

    // Stores a new active master key for the owner, or returns the active row that another node stored first.
    private async ValueTask<(PersistedMasterKey Row, MasterKey? CreatedKey)> CreateActiveRowAsync(
        SecretOwner key,
        IStoreManager storeManager,
        IMasterKeyStore store,
        CancellationToken cancellationToken
    )
    {
        var version = await store.GetLatestVersionAsync(key, cancellationToken) + 1;
        var created = await Factory.CreateAsync(key, version, cancellationToken);
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
