// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Persistence.Stores;

namespace NCode.Identity.Secrets.Persistence.Protection;

/// <summary>
/// An in-memory <see cref="IStoreManagerFactory"/> that serves the owner root key store and the master key store, with
/// the concurrency-token behavior of a database: a row is read as a copy, and an update with a stale token fails.
/// </summary>
public sealed class InMemoryOwnerRootKeyStores : IStoreManagerFactory
{
    private readonly List<PersistedOwnerRootKey> _rootKeys = [];

    private readonly List<PersistedMasterKey> _masterKeys = [];

    /// <summary>
    /// Gets the number of units of work that were saved.
    /// </summary>
    public int SaveCount { get; private set; }

    /// <summary>
    /// Gets the stored root keys.
    /// </summary>
    public IReadOnlyList<PersistedOwnerRootKey> RootKeys => _rootKeys;

    /// <summary>
    /// Gets the stored master keys.
    /// </summary>
    public IReadOnlyList<PersistedMasterKey> MasterKeys => _masterKeys;

    /// <summary>
    /// Stores a copy of the root key.
    /// </summary>
    public PersistedOwnerRootKey Seed(PersistedOwnerRootKey rootKey)
    {
        rootKey.ConcurrencyToken = Guid.NewGuid().ToString("N");
        _rootKeys.Add(Copy(rootKey));
        return rootKey;
    }

    /// <summary>
    /// Stores the master key.
    /// </summary>
    public void Seed(PersistedMasterKey masterKey) => _masterKeys.Add(masterKey);

    /// <inheritdoc />
    public ValueTask<IStoreManager> CreateAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult<IStoreManager>(new Manager(this));

    private static PersistedOwnerRootKey Copy(PersistedOwnerRootKey source) =>
        new()
        {
            OwnerType = source.OwnerType,
            OwnerId = source.OwnerId,
            KeyWrapperName = source.KeyWrapperName,
            KeyReference = source.KeyReference,
            State = source.State,
            CreatedWhen = source.CreatedWhen,
            StateChangedWhen = source.StateChangedWhen,
            ValidatedWhen = source.ValidatedWhen,
            FailedWhen = source.FailedWhen,
            Failure = source.Failure,
            ConcurrencyToken = source.ConcurrencyToken,
        };

    private sealed class Manager(InMemoryOwnerRootKeyStores owner) : IStoreManager
    {
        private readonly List<Action> _pending = [];

        public T GetStore<T>()
            where T : IStore
        {
            object store =
                typeof(T) == typeof(IOwnerRootKeyStore) ? new RootKeyStore(owner, _pending)
                : typeof(T) == typeof(IMasterKeyStore) ? new MasterKeyStore(owner)
                : throw new NotSupportedException(typeof(T).Name);

            return (T)store;
        }

        public ValueTask SaveChangesAsync(CancellationToken cancellationToken)
        {
            foreach (var action in _pending)
            {
                action();
            }

            _pending.Clear();
            owner.SaveCount++;
            return ValueTask.CompletedTask;
        }

        public object? GetService(Type serviceType) => null;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class RootKeyStore(InMemoryOwnerRootKeyStores owner, List<Action> pending)
        : IOwnerRootKeyStore
    {
        public TStore GetStore<TStore>()
            where TStore : IStore => throw new NotSupportedException();

        public object? GetService(Type serviceType) => null;

        private PersistedOwnerRootKey? Find(string ownerType, string ownerId) =>
            owner._rootKeys.SingleOrDefault(row =>
                string.Equals(row.OwnerType, ownerType, StringComparison.OrdinalIgnoreCase)
                && string.Equals(row.OwnerId, ownerId, StringComparison.OrdinalIgnoreCase)
            );

        public ValueTask AddAsync(PersistedOwnerRootKey item, CancellationToken cancellationToken)
        {
            pending.Add(() =>
            {
                if (Find(item.OwnerType, item.OwnerId) is not null)
                    throw new InvalidOperationException("The owner already has a root key.");

                item.ConcurrencyToken = Guid.NewGuid().ToString("N");
                owner._rootKeys.Add(Copy(item));
            });
            return ValueTask.CompletedTask;
        }

        public ValueTask UpdateAsync(
            PersistedOwnerRootKey item,
            CancellationToken cancellationToken
        )
        {
            pending.Add(() =>
            {
                var stored =
                    Find(item.OwnerType, item.OwnerId)
                    ?? throw new InvalidOperationException("The root key no longer exists.");
                if (
                    !string.Equals(
                        stored.ConcurrencyToken,
                        item.ConcurrencyToken,
                        StringComparison.Ordinal
                    )
                )
                    throw new StoreConcurrencyException("The root key changed.");

                // The store renews the token of the row, and the item keeps the token it was read with.
                var renewed = Copy(item);
                renewed.ConcurrencyToken = Guid.NewGuid().ToString("N");
                owner._rootKeys[owner._rootKeys.IndexOf(stored)] = renewed;
            });
            return ValueTask.CompletedTask;
        }

        public ValueTask<PersistedOwnerRootKey?> FindAsync(
            SecretOwner secretOwner,
            CancellationToken cancellationToken
        )
        {
            var stored = Find(secretOwner.OwnerType, secretOwner.OwnerId);
            return ValueTask.FromResult(stored is null ? null : Copy(stored));
        }

        public ValueTask<PagedResult<PersistedOwnerRootKey>> GetDuePageAsync(
            DateTimeOffset validatedBefore,
            DateTimeOffset retryBefore,
            PageRequest page,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public ValueTask<bool> RemoveAsync(
            PersistedOwnerRootKey item,
            CancellationToken cancellationToken
        )
        {
            var stored = Find(item.OwnerType, item.OwnerId);
            if (stored is null)
                return ValueTask.FromResult(false);

            if (
                !string.Equals(
                    stored.ConcurrencyToken,
                    item.ConcurrencyToken,
                    StringComparison.Ordinal
                )
            )
                throw new StoreConcurrencyException("The root key changed.");

            pending.Add(() => owner._rootKeys.Remove(stored));
            return ValueTask.FromResult(true);
        }
    }

    private sealed class MasterKeyStore(InMemoryOwnerRootKeyStores owner) : IMasterKeyStore
    {
        public TStore GetStore<TStore>()
            where TStore : IStore => throw new NotSupportedException();

        public object? GetService(Type serviceType) => null;

        public ValueTask AddAsync(PersistedMasterKey item, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public ValueTask UpdateAsync(
            PersistedMasterKey item,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<PersistedMasterKey>> ListAsync(
            SecretOwner secretOwner,
            CancellationToken cancellationToken
        ) =>
            ValueTask.FromResult<IReadOnlyList<PersistedMasterKey>>([
                .. owner._masterKeys.Where(row =>
                    string.Equals(
                        row.OwnerType,
                        secretOwner.OwnerType,
                        StringComparison.OrdinalIgnoreCase
                    )
                    && string.Equals(
                        row.OwnerId,
                        secretOwner.OwnerId,
                        StringComparison.OrdinalIgnoreCase
                    )
                ),
            ]);

        public ValueTask<PersistedMasterKey?> FindActiveAsync(
            SecretOwner secretOwner,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<PersistedMasterKey>> FindByKeyIdsAsync(
            SecretOwner secretOwner,
            IReadOnlyCollection<Guid> keyIds,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public ValueTask<int> GetLatestVersionAsync(
            SecretOwner secretOwner,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public ValueTask<int> RemoveAllAsync(
            SecretOwner secretOwner,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public ValueTask<PagedResult<PersistedMasterKey>> GetPageAsync(
            PageRequest page,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public ValueTask<PagedResult<PersistedMasterKey>> GetActivePageAsync(
            DateTimeOffset createdBefore,
            PageRequest page,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public ValueTask<PagedResult<SecretOwner>> GetRetiredOwnerPageAsync(
            PageRequest page,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();

        public ValueTask<bool> RemoveAsync(
            PersistedMasterKey item,
            CancellationToken cancellationToken
        ) => throw new NotSupportedException();
    }
}
