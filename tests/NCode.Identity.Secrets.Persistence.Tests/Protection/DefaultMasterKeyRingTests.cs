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

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Persistence.Stores;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class DefaultMasterKeyRingTests : IDisposable
{
    // The mixed case proves that the ring matches owners without regard to case.
    private static readonly SecretOwner Owner = SecretOwner.ForTenant("Tenant-A");

    private static readonly SecretOwner NormalizedOwner = Owner.Normalize();

    // A second owner, to prove that different owners do not wait for each other.
    private static readonly SecretOwner OtherOwner = SecretOwner.ForTenant("Tenant-B");

    private static readonly SecretOwner NormalizedOtherOwner = OtherOwner.Normalize();

    private static readonly SecretProtectionContext Context =
        SecretProtectionContext.ForPersistedSecret("secret-1");

    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly Mock<IStoreManagerFactory> _storeManagerFactory;
    private readonly Queue<IStoreManager> _storeManagers = new();
    private bool _storeManagerFactorySetUp;
    private readonly EphemeralRootKeyProvider _provider = new();
    private readonly RecoveryKeyWrapper _recovery = new(
        Microsoft.Extensions.Options.Options.Create(
            new SecretProtectionOptions { Recovery = new RecoveryKeyOptions { Disabled = true } }
        )
    );
    private readonly MasterKeyFactory _factory;

    public DefaultMasterKeyRingTests()
    {
        var catalog = new DefaultRootKeyProviderCatalog(
            [_provider],
            Microsoft.Extensions.Options.Options.Create(new SecretProtectionOptions())
        );
        _factory = new MasterKeyFactory(catalog, _recovery, TimeProvider.System);
        _storeManagerFactory = _mocks.Create<IStoreManagerFactory>();
    }

    public void Dispose()
    {
        _recovery.Dispose();
        _provider.Dispose();
        _mocks.Verify();
        Assert.Empty(_storeManagers);
    }

    private DefaultMasterKeyRing CreateRing() =>
        new(
            _factory,
            _storeManagerFactory.Object,
            _recovery,
            NullLogger<DefaultMasterKeyRing>.Instance
        );

    // Every store manager that the ring is expected to create is queued by a test, so a read that the test does not
    // expect fails because the queue is empty, and a read that never happens fails the check on dispose. A test that
    // queues no store manager leaves the factory without a setup, so the strict mock fails it if the ring reads the
    // store.
    private void SetupStoreManagerFactory() =>
        _storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<IStoreManager>(_storeManagers.Dequeue()))
            .Verifiable();

    // Queues a store manager whose only expected members are the master key store and its disposal.
    private (Mock<IMasterKeyStore> Store, Mock<IStoreManager> Manager) EnqueueStoreManager()
    {
        if (!_storeManagerFactorySetUp)
        {
            SetupStoreManagerFactory();
            _storeManagerFactorySetUp = true;
        }

        var store = _mocks.Create<IMasterKeyStore>();
        var manager = _mocks.Create<IStoreManager>();
        manager.Setup(x => x.GetStore<IMasterKeyStore>()).Returns(store.Object).Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        _storeManagers.Enqueue(manager.Object);
        return (store, manager);
    }

    private static void SetupFindActive(Mock<IMasterKeyStore> store, PersistedMasterKey? row) =>
        store
            .Setup(x => x.FindActiveAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PersistedMasterKey?>(row))
            .Verifiable();

    private static void SetupGetLatestVersion(Mock<IMasterKeyStore> store, int latestVersion) =>
        store
            .Setup(x => x.GetLatestVersionAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<int>(latestVersion))
            .Verifiable();

    // The setup matches only a set of exactly one identifier, so a query for any other set fails the test.
    private static void SetupFindByKeyId(
        Mock<IMasterKeyStore> store,
        Guid keyId,
        params PersistedMasterKey[] rows
    ) =>
        store
            .Setup(x =>
                x.FindByKeyIdsAsync(
                    NormalizedOwner,
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(keyId)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>(rows))
            .Verifiable();

    // Queues a store manager that reads the specified active row and nothing else.
    private void ExpectFindActive(PersistedMasterKey? activeRow)
    {
        var (store, _) = EnqueueStoreManager();
        SetupFindActive(store, activeRow);
    }

    // Queues a store manager whose read of the active row of the specified normalized owner (the owner of the tests by
    // default) signals that it started, waits until the test releases it or the caller is cancelled, and then returns
    // the specified row. The bounded wait makes a regression fail instead of hang.
    private void ExpectFindActiveBlocked(
        PersistedMasterKey activeRow,
        TaskCompletionSource started,
        TaskCompletionSource release,
        SecretOwner? normalizedOwner = null
    )
    {
        var owner = normalizedOwner ?? NormalizedOwner;
        var (store, _) = EnqueueStoreManager();
        store
            .Setup(x => x.FindActiveAsync(owner, It.IsAny<CancellationToken>()))
            .Returns(
                async (SecretOwner _, CancellationToken cancellationToken) =>
                {
                    started.TrySetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                    return activeRow;
                }
            )
            .Verifiable();
    }

    // Queues a store manager that finds no active row, reports the latest version, accepts the new row and saves it.
    // The returned list receives the row.
    private List<PersistedMasterKey> ExpectCreate(int latestVersion = 0)
    {
        var (store, manager) = EnqueueStoreManager();
        SetupFindActive(store, null);
        SetupGetLatestVersion(store, latestVersion);

        var added = new List<PersistedMasterKey>();
        store
            .Setup(x => x.AddAsync(It.IsAny<PersistedMasterKey>(), It.IsAny<CancellationToken>()))
            .Callback<PersistedMasterKey, CancellationToken>((row, _) => added.Add(row))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        return added;
    }

    // Queues a store manager that finds no active row, accepts the new row, and fails to save it.
    private void ExpectCreateFailsToSave(Exception failure)
    {
        var (store, manager) = EnqueueStoreManager();
        SetupFindActive(store, null);
        SetupGetLatestVersion(store, 0);

        store
            .Setup(x => x.AddAsync(It.IsAny<PersistedMasterKey>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Throws(failure)
            .Verifiable();
    }

    // Creates a row that the ring can unwrap because it is wrapped by the same ephemeral root key. The row belongs to
    // the specified normalized owner, which is the owner of the tests by default, because the root key context binds it.
    private async Task<PersistedMasterKey> CreateRowAsync(
        int version = 1,
        bool retired = false,
        SecretOwner? normalizedOwner = null
    )
    {
        var created = await _factory.CreateAsync(
            normalizedOwner ?? NormalizedOwner,
            version,
            CancellationToken.None
        );
        created.Key.Dispose();

        if (retired)
            created.Row.RetiredWhen = DateTimeOffset.UtcNow;

        return created.Row;
    }

    private static string CreateEnvelopeFor(Guid keyId)
    {
        var buffer = new byte[SecretProtectionFormat.HeaderSizeBytes];
        SecretProtectionFormat.WriteMasterKeyId(
            keyId,
            buffer.AsSpan(0, SecretProtectionFormat.KeyIdSizeBytes)
        );
        return SecretProtectionFormat.Prefix + Base64Url.EncodeToString(buffer);
    }

    private static Guid ReadKeyId(string protectedValue)
    {
        Assert.True(SecretProtectionFormat.TryReadMasterKeyId(protectedValue, out var keyId));
        return keyId;
    }

    private static byte[] Unprotect(ISecretProtector protector, string protectedValue)
    {
        var writer = new ArrayBufferWriter<byte>();
        protector.Unprotect(protectedValue, Context, ref writer);
        return writer.WrittenSpan.ToArray();
    }

    #region GetProtectorAsync Tests

    [Fact]
    public async Task GetProtector_DefaultOwner_Throws()
    {
        // No store manager is queued, so a read of the store would fail the test.
        using var ring = CreateRing();

        var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await ring.GetProtectorAsync(default, [], CancellationToken.None)
        );

        Assert.Equal("owner", exception.ParamName);
    }

    [Fact]
    public async Task GetProtector_NoKeys_CreatesVersionOne()
    {
        var added = ExpectCreate();
        using var ring = CreateRing();

        var protector = await ring.GetProtectorAsync(Owner, [], CancellationToken.None);

        var row = Assert.Single(added);
        Assert.Equal(1, row.Version);
        Assert.Null(row.RetiredWhen);
        Assert.Equal(NormalizedOwner, row.Owner);
        Assert.Equal(Owner, protector.Owner);

        var protectedValue = protector.ProtectToString("secret"u8, Context);

        Assert.Equal(row.KeyId, ReadKeyId(protectedValue));
        Assert.Equal("secret"u8.ToArray(), Unprotect(protector, protectedValue));
    }

    [Fact]
    public async Task GetProtector_Cached_DoesNotReadStore()
    {
        ExpectCreate();
        using var ring = CreateRing();
        var first = await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        var protectedValue = first.ProtectToString("secret"u8, Context);

        // No store manager is queued for the second call, so a read of the store would fail the test.
        var second = await ring.GetProtectorAsync(Owner, [protectedValue], CancellationToken.None);

        Assert.Equal("secret"u8.ToArray(), Unprotect(second, protectedValue));
        Assert.Equal(
            ReadKeyId(protectedValue),
            ReadKeyId(second.ProtectToString("secret"u8, Context))
        );
    }

    [Fact]
    public async Task GetProtector_ExistingKey_UnwrapsAndProtects()
    {
        var added = ExpectCreate();
        using var writerRing = CreateRing();
        var writer = await writerRing.GetProtectorAsync(Owner, [], CancellationToken.None);
        var protectedValue = writer.ProtectToString("secret"u8, Context);
        var row = Assert.Single(added);

        ExpectFindActive(row);
        using var readerRing = CreateRing();
        var reader = await readerRing.GetProtectorAsync(
            Owner,
            [protectedValue],
            CancellationToken.None
        );

        Assert.Equal("secret"u8.ToArray(), Unprotect(reader, protectedValue));
        Assert.Equal(row.KeyId, ReadKeyId(reader.ProtectToString("secret"u8, Context)));
    }

    [Fact]
    public async Task GetProtector_RequiredUnknownKeyId_ReloadsThenThrows()
    {
        var added = ExpectCreate();
        using var ring = CreateRing();
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        var row = Assert.Single(added);

        // The cached owner lacks the required key, so the ring queries the store for exactly that key before it gives up.
        var unknownKeyId = Guid.NewGuid();
        var (store, _) = EnqueueStoreManager();
        SetupFindActive(store, row);
        SetupFindByKeyId(store, unknownKeyId);
        var unknownValue = CreateEnvelopeFor(unknownKeyId);

        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
            await ring.GetProtectorAsync(Owner, [unknownValue], CancellationToken.None)
        );

        Assert.Contains(unknownKeyId.ToString(), exception.Message);
        Assert.Contains(NormalizedOwner.ToString(), exception.Message);
    }

    [Fact]
    public async Task GetProtector_RetiredKeyRequired_LoadsOnlyThatKey()
    {
        var active = await CreateRowAsync(version: 2);
        var retired = await CreateRowAsync(version: 1, retired: true);

        // The setup of the query matches only the retired key's identifier, so the active row is not queried twice.
        var (store, _) = EnqueueStoreManager();
        SetupFindActive(store, active);
        SetupFindByKeyId(store, retired.KeyId, retired);
        using var ring = CreateRing();

        var protector = await ring.GetProtectorAsync(
            Owner,
            [CreateEnvelopeFor(retired.KeyId)],
            CancellationToken.None
        );

        Assert.Equal(active.KeyId, ReadKeyId(protector.ProtectToString("secret"u8, Context)));
    }

    [Fact]
    public async Task GetProtector_ActiveCachedRetiredMissing_DoesNotReloadActive()
    {
        var added = ExpectCreate(latestVersion: 1);
        using var ring = CreateRing();
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        var active = Assert.Single(added);
        Assert.Equal(2, active.Version);

        // Unwrapping the row again would throw, so the ring must reuse the cached active key.
        active.RootWrappedKey = "corrupted";
        var retired = await CreateRowAsync(version: 1, retired: true);
        var (store, _) = EnqueueStoreManager();
        SetupFindActive(store, active);
        SetupFindByKeyId(store, retired.KeyId, retired);

        var protector = await ring.GetProtectorAsync(
            Owner,
            [CreateEnvelopeFor(retired.KeyId)],
            CancellationToken.None
        );

        Assert.Equal(active.KeyId, ReadKeyId(protector.ProtectToString("secret"u8, Context)));
    }

    [Fact]
    public async Task GetProtector_SaveFailsAndWinnerExists_UsesWinner()
    {
        var winner = await CreateRowAsync();
        ExpectCreateFailsToSave(new InvalidOperationException("The master key already exists."));
        ExpectFindActive(winner);
        using var ring = CreateRing();

        var protector = await ring.GetProtectorAsync(Owner, [], CancellationToken.None);

        var protectedValue = protector.ProtectToString("secret"u8, Context);
        Assert.Equal(winner.KeyId, ReadKeyId(protectedValue));
        Assert.Equal("secret"u8.ToArray(), Unprotect(protector, protectedValue));
    }

    [Fact]
    public async Task GetProtector_SaveFailsAndNoWinner_Rethrows()
    {
        var failure = new InvalidOperationException("The database is unavailable.");
        ExpectCreateFailsToSave(failure);
        ExpectFindActive(null);
        using var ring = CreateRing();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ring.GetProtectorAsync(Owner, [], CancellationToken.None)
        );

        Assert.Same(failure, exception);
    }

    [Fact]
    public async Task GetProtector_OwnerDifferentCase_SharesEntry()
    {
        ExpectCreate();
        using var ring = CreateRing();
        var first = await ring.GetProtectorAsync(
            SecretOwner.ForTenant("Tenant-A"),
            [],
            CancellationToken.None
        );
        var protectedValue = first.ProtectToString("secret"u8, Context);

        // No store manager is queued for the second call, so a read of the store would fail the test.
        var second = await ring.GetProtectorAsync(
            SecretOwner.ForTenant("TENANT-a"),
            [protectedValue],
            CancellationToken.None
        );

        Assert.Equal("secret"u8.ToArray(), Unprotect(second, protectedValue));
    }

    [Fact]
    public async Task GetProtector_ConcurrentCallersSameOwner_LoadsOnce()
    {
        const int callerCount = 8;
        var row = await CreateRowAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // Only one store manager is queued, so a second load of the owner fails because the queue is empty.
        ExpectFindActiveBlocked(row, started, release);
        using var ring = CreateRing();

        // The first call blocks in its read and every other call waits for the owner's gate, so all calls are in
        // flight before the read is released.
        var loads = Enumerable
            .Range(0, callerCount)
            .Select(_ => ring.GetProtectorAsync(Owner, [], CancellationToken.None).AsTask())
            .ToArray();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        release.SetResult();
        var protectors = await Task.WhenAll(loads).WaitAsync(TimeSpan.FromSeconds(10));

        // A value that one caller's protector protects unprotects under every other caller's protector.
        Assert.Equal(callerCount, protectors.Length);
        var protectedValue = protectors[0].ProtectToString("secret"u8, Context);
        Assert.Equal(row.KeyId, ReadKeyId(protectedValue));
        foreach (var protector in protectors)
        {
            Assert.Equal(Owner, protector.Owner);
            Assert.Equal("secret"u8.ToArray(), Unprotect(protector, protectedValue));
        }
    }

    [Fact]
    public async Task GetProtector_DifferentOwners_LoadInParallel()
    {
        var row = await CreateRowAsync();
        var otherRow = await CreateRowAsync(normalizedOwner: NormalizedOtherOwner);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var otherStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The queue is first in, first out, and each call creates its store manager before the next call starts.
        ExpectFindActiveBlocked(row, started, release);
        ExpectFindActiveBlocked(otherRow, otherStarted, release, NormalizedOtherOwner);
        using var ring = CreateRing();

        var load = ring.GetProtectorAsync(Owner, [], CancellationToken.None).AsTask();
        var otherLoad = ring.GetProtectorAsync(OtherOwner, [], CancellationToken.None).AsTask();

        // Both reads start before either is released, so the owners do not wait for each other.
        await Task.WhenAll(started.Task, otherStarted.Task).WaitAsync(TimeSpan.FromSeconds(10));

        release.SetResult();
        var protector = await load.WaitAsync(TimeSpan.FromSeconds(10));
        var otherProtector = await otherLoad.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(Owner, protector.Owner);
        Assert.Equal(OtherOwner, otherProtector.Owner);
        Assert.Equal(row.KeyId, ReadKeyId(protector.ProtectToString("secret"u8, Context)));
        Assert.Equal(
            otherRow.KeyId,
            ReadKeyId(otherProtector.ProtectToString("secret"u8, Context))
        );
    }

    [Fact]
    public async Task GetProtector_LoadCancelled_NextCallerLoads()
    {
        var row = await CreateRowAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        // The read is never released, so only the cancellation of the caller ends it.
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ExpectFindActiveBlocked(row, started, release);
        using var ring = CreateRing();
        using var cancellation = new CancellationTokenSource();

        var cancelled = ring.GetProtectorAsync(Owner, [], cancellation.Token).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            cancelled.WaitAsync(TimeSpan.FromSeconds(10))
        );

        // The cancelled caller released the owner's gate, so the next caller loads the owner and does not wait.
        ExpectFindActive(row);
        var protector = await ring.GetProtectorAsync(Owner, [], CancellationToken.None)
            .AsTask()
            .WaitAsync(TimeSpan.FromSeconds(10));

        var protectedValue = protector.ProtectToString("secret"u8, Context);
        Assert.Equal(row.KeyId, ReadKeyId(protectedValue));
        Assert.Equal("secret"u8.ToArray(), Unprotect(protector, protectedValue));
    }

    #endregion

    #region Evict Tests

    [Fact]
    public void Evict_DefaultOwner_Throws()
    {
        using var ring = CreateRing();

        var exception = Assert.Throws<ArgumentException>(() => ring.Evict(default));

        Assert.Equal("owner", exception.ParamName);
    }

    [Fact]
    public async Task Evict_ThenGetProtector_ReloadsStore()
    {
        var added = ExpectCreate();
        using var ring = CreateRing();
        var before = await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        var protectedValue = before.ProtectToString("secret"u8, Context);
        var row = Assert.Single(added);

        ring.Evict(SecretOwner.ForTenant("TENANT-a"));

        ExpectFindActive(row);
        var after = await ring.GetProtectorAsync(Owner, [protectedValue], CancellationToken.None);

        Assert.Equal("secret"u8.ToArray(), Unprotect(after, protectedValue));

        // A protector that was handed out before the eviction stays usable because eviction does not zero the keys.
        Assert.Equal("secret"u8.ToArray(), Unprotect(before, protectedValue));
    }

    [Fact]
    public async Task EvictAll_ThenGetProtector_ReloadsStore()
    {
        var added = ExpectCreate();
        using var ring = CreateRing();
        var before = await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        var protectedValue = before.ProtectToString("secret"u8, Context);
        var row = Assert.Single(added);

        ring.EvictAll();

        ExpectFindActive(row);
        var after = await ring.GetProtectorAsync(Owner, [protectedValue], CancellationToken.None);

        Assert.Equal("secret"u8.ToArray(), Unprotect(after, protectedValue));

        // A protector that was handed out before the eviction stays usable because eviction does not zero the keys.
        Assert.Equal("secret"u8.ToArray(), Unprotect(before, protectedValue));
    }

    [Fact]
    public async Task Evict_DuringLoad_DoesNotCacheTheLoadedKeys()
    {
        var row = await CreateRowAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ExpectFindActiveBlocked(row, started, release);
        using var ring = CreateRing();

        var load = ring.GetProtectorAsync(Owner, [], CancellationToken.None).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        ring.Evict(SecretOwner.ForTenant("TENANT-a"));
        release.SetResult();
        var during = await load.WaitAsync(TimeSpan.FromSeconds(10));

        // The protector of the load that the eviction overtook stays usable because eviction does not zero the keys.
        var protectedValue = during.ProtectToString("secret"u8, Context);
        Assert.Equal(row.KeyId, ReadKeyId(protectedValue));
        Assert.Equal("secret"u8.ToArray(), Unprotect(during, protectedValue));

        // The evicted keys were not cached, so the next call reads the store again.
        ExpectFindActive(row);
        var after = await ring.GetProtectorAsync(Owner, [protectedValue], CancellationToken.None);

        Assert.Equal("secret"u8.ToArray(), Unprotect(after, protectedValue));
    }

    [Fact]
    public async Task EvictAll_DuringLoad_DoesNotCacheTheLoadedKeys()
    {
        var row = await CreateRowAsync();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ExpectFindActiveBlocked(row, started, release);
        using var ring = CreateRing();

        var load = ring.GetProtectorAsync(Owner, [], CancellationToken.None).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        ring.EvictAll();
        release.SetResult();
        var during = await load.WaitAsync(TimeSpan.FromSeconds(10));

        // The protector of the load that the eviction overtook stays usable because eviction does not zero the keys.
        var protectedValue = during.ProtectToString("secret"u8, Context);
        Assert.Equal(row.KeyId, ReadKeyId(protectedValue));
        Assert.Equal("secret"u8.ToArray(), Unprotect(during, protectedValue));

        // The evicted keys were not cached, so the next call reads the store again.
        ExpectFindActive(row);
        var after = await ring.GetProtectorAsync(Owner, [protectedValue], CancellationToken.None);

        Assert.Equal("secret"u8.ToArray(), Unprotect(after, protectedValue));
    }

    #endregion
}
