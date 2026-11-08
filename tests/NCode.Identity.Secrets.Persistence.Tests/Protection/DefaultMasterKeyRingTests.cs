// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Buffers.Text;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Logging;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Persistence.Stores;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class DefaultMasterKeyRingTests : BaseTests
{
    // The mixed case proves that the ring matches owners without regard to case.
    private static readonly SecretOwner Owner = SecretOwner.ForTenant("Tenant-A");

    private static readonly SecretOwner NormalizedOwner = Owner.Normalize();

    // A second owner, to prove that different owners do not wait for each other.
    private static readonly SecretOwner OtherOwner = SecretOwner.ForTenant("Tenant-B");

    private static readonly SecretOwner NormalizedOtherOwner = OtherOwner.Normalize();

    private static readonly SecretProtectionContext Context =
        SecretProtectionContext.ForPersistedSecret("secret-1");

    private readonly Mock<IStoreManagerFactory> _storeManagerFactory;
    private readonly Queue<IStoreManager> _storeManagers = new();
    private bool _storeManagerFactorySetUp;
    private readonly EphemeralKeyWrapper _provider = new();
    private readonly RecoveryKeyWrapper _recovery = new(
        Microsoft.Extensions.Options.Options.Create(
            new SecretProtectionOptions { Recovery = new RecoveryKeyOptions { Disabled = true } }
        )
    );
    private readonly MasterKeyFactory _factory;

    public DefaultMasterKeyRingTests()
    {
        var catalog = new DefaultKeyWrapperCatalog(
            [_provider],
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RootKeyProvider = KeyWrapperNames.Ephemeral }
            )
        );
        _factory = new MasterKeyFactory(catalog, _recovery, TimeProvider.System);
        _storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _recovery.Dispose();
            _provider.Dispose();
        }

        base.Dispose(disposing);

        if (disposing)
        {
            Assert.Empty(_storeManagers);
        }
    }

    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero)
    );

    private DefaultMasterKeyRing CreateRing() =>
        new(
            _factory,
            _storeManagerFactory.Object,
            _recovery,
            Microsoft.Extensions.Options.Options.Create(new SecretProtectionOptions()),
            _time,
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

        var store = CreateStrictMock<IMasterKeyStore>();
        var manager = CreateStrictMock<IStoreManager>();
        manager.Setup(x => x.GetStore<IMasterKeyStore>()).Returns(store.Object).Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        _storeManagers.Enqueue(manager.Object);
        return (store, manager);
    }

    // Gives the store manager an owner root key store whose read of the specified normalized owner returns the
    // specified root key. The ring reads it only when it creates a key or reads a row under an owner root key.
    private void SetupRootKey(
        Mock<IStoreManager> manager,
        PersistedOwnerRootKey? rootKey,
        SecretOwner? normalizedOwner = null
    )
    {
        var rootKeyStore = CreateStrictMock<IOwnerRootKeyStore>();
        rootKeyStore
            .Setup(x =>
                x.FindAsync(normalizedOwner ?? NormalizedOwner, It.IsAny<CancellationToken>())
            )
            .Returns(new ValueTask<PersistedOwnerRootKey?>(rootKey))
            .Verifiable();
        manager
            .Setup(x => x.GetStore<IOwnerRootKeyStore>())
            .Returns(rootKeyStore.Object)
            .Verifiable();
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
        SetupRootKey(manager, null);
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
        SetupRootKey(manager, null);
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
            rootKey: null,
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

    #region TryReadMasterKeyId Tests

    [Fact]
    public void TryReadMasterKeyId_Envelope_ReturnsKeyId()
    {
        var keyId = Guid.NewGuid();

        Assert.True(CreateRing().TryReadMasterKeyId(CreateEnvelopeFor(keyId), out var result));
        Assert.Equal(keyId, result);
    }

    [Fact]
    public void TryReadMasterKeyId_OtherText_ReturnsFalse()
    {
        Assert.False(CreateRing().TryReadMasterKeyId("not an envelope", out _));
    }

    #endregion

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
    public async Task GetProtector_StaleEntry_ReloadsWithoutUnwrappingAgain()
    {
        var added = ExpectCreate();
        using var ring = CreateRing();
        var first = await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        var protectedValue = first.ProtectToString("secret"u8, Context);
        var row = Assert.Single(added);

        // The active row is read again and the stored key is reused: a different wrapped key would not unwrap.
        ExpectFindActive(row);
        _time.Advance(TimeSpan.FromMinutes(6));
        var second = await ring.GetProtectorAsync(Owner, [protectedValue], CancellationToken.None);

        Assert.Equal("secret"u8.ToArray(), Unprotect(second, protectedValue));
    }

    [Fact]
    public async Task GetProtector_FreshEntry_DoesNotReadStore()
    {
        ExpectCreate();
        using var ring = CreateRing();
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);

        // No store manager is queued, so a read of the store would fail the test.
        _time.Advance(TimeSpan.FromMinutes(4));
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
    }

    [Fact]
    public async Task GetProtector_CachedKeyWhoseRowVanished_IsDropped()
    {
        var added = ExpectCreate();
        using var ring = CreateRing();
        var first = await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        var oldValue = first.ProtectToString("secret"u8, Context);
        var oldRow = Assert.Single(added);

        // The owner rotated: a new active row exists, the old row is retired and then destroyed.
        var newRow = await CreateRowAsync(version: 2);
        _time.Advance(TimeSpan.FromMinutes(6));
        var (store, _) = EnqueueStoreManager();
        SetupFindActive(store, newRow);
        SetupFindByKeyId(store, oldRow.KeyId);

        var second = await ring.GetProtectorAsync(Owner, [], CancellationToken.None);

        Assert.Equal(newRow.KeyId, ReadKeyId(second.ProtectToString("secret"u8, Context)));
        Assert.Throws<CryptographicException>(() => Unprotect(second, oldValue));
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

    #region Owner Root Key Tests

    private const string RootKeyReference = "kms://tenant-a/root";

    private readonly FakeReferencedKeyWrapper _referenced = new();

    private MasterKeyFactory CreateOwnerRootFactory(IReferencedKeyWrapper? referenced = null)
    {
        var catalog = new DefaultKeyWrapperCatalog(
            [_provider, referenced ?? _referenced],
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RootKeyProvider = KeyWrapperNames.Ephemeral }
            )
        );
        return new MasterKeyFactory(catalog, _recovery, _time);
    }

    private DefaultMasterKeyRing CreateOwnerRootRing(
        MasterKeyFactory factory,
        ILogger<DefaultMasterKeyRing>? logger = null
    ) =>
        new(
            factory,
            _storeManagerFactory.Object,
            _recovery,
            Microsoft.Extensions.Options.Options.Create(new SecretProtectionOptions()),
            _time,
            logger ?? NullLogger<DefaultMasterKeyRing>.Instance
        );

    private PersistedOwnerRootKey CreateRootKey(
        string state = OwnerRootKeyStates.Active,
        string keyReference = RootKeyReference,
        string? accessIdentity = null
    ) =>
        new()
        {
            OwnerType = NormalizedOwner.OwnerType,
            OwnerId = NormalizedOwner.OwnerId,
            KeyWrapperName = _referenced.Name,
            KeyReference = keyReference,
            AccessIdentity = accessIdentity,
            State = state,
            CreatedWhen = _time.GetUtcNow(),
            StateChangedWhen = _time.GetUtcNow(),
        };

    private async Task<PersistedMasterKey> CreateOwnerRootedRowAsync(
        MasterKeyFactory factory,
        int version = 1,
        string keyReference = RootKeyReference,
        string? accessIdentity = null
    )
    {
        var created = await factory.CreateAsync(
            NormalizedOwner,
            version,
            CreateRootKey(keyReference: keyReference, accessIdentity: accessIdentity),
            CancellationToken.None
        );
        created.Key.Dispose();
        return created.Row;
    }

    // Queues a store manager that reads the active row, the specified rows and the owner's root key. The rows are
    // returned for a query of exactly the specified identifiers.
    private void ExpectLoadWithRows(
        PersistedMasterKey activeRow,
        PersistedOwnerRootKey rootKey,
        params PersistedMasterKey[] rows
    )
    {
        var (store, manager) = EnqueueStoreManager();
        SetupFindActive(store, activeRow);
        var keyIds = rows.Select(row => row.KeyId).ToArray();
        store
            .Setup(x =>
                x.FindByKeyIdsAsync(
                    NormalizedOwner,
                    It.Is<IReadOnlyCollection<Guid>>(ids =>
                        ids.Count == keyIds.Length && keyIds.All(keyId => ids.Contains(keyId))
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>(rows))
            .Verifiable();
        SetupRootKey(manager, rootKey);
    }

    // Queues a store manager that reads the active row and the owner's root key.
    private void ExpectLoad(PersistedMasterKey activeRow, PersistedOwnerRootKey? rootKey)
    {
        var (store, manager) = EnqueueStoreManager();
        SetupFindActive(store, activeRow);
        SetupRootKey(manager, rootKey);
    }

    [Fact]
    public async Task GetProtector_NoKeysAndAnActiveOwnerRootKey_CreatesTheKeyUnderItWithoutARecoveryCopy()
    {
        var factory = CreateOwnerRootFactory();
        var (store, manager) = EnqueueStoreManager();
        SetupFindActive(store, null);
        SetupRootKey(manager, CreateRootKey());
        SetupGetLatestVersion(store, 0);
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
        using var ring = CreateOwnerRootRing(factory);

        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);

        var row = Assert.Single(added);
        Assert.Equal(_referenced.Name, row.RootKeyProvider);
        Assert.Equal(RootKeyReference, row.RootKeyReference);
        Assert.Null(row.RecoveryWrappedKey);
    }

    [Fact]
    public async Task GetProtector_OwnerRootedEntry_IsReusedWithinTheValidationIntervalAndUnwrappedAgainAfterIt()
    {
        var factory = CreateOwnerRootFactory();
        var row = await CreateOwnerRootedRowAsync(factory);
        var rootKey = CreateRootKey();
        ExpectLoad(row, rootKey);
        using var ring = CreateOwnerRootRing(factory);
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        Assert.Equal(1, _referenced.UnwrapCount);

        // A refresh after the refresh interval reads the rows but reuses the material, so nothing is unwrapped.
        _time.Advance(TimeSpan.FromMinutes(6));
        ExpectLoad(row, rootKey);
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        Assert.Equal(1, _referenced.UnwrapCount);

        // The material is as old as the validation interval, so every key is unwrapped again.
        _time.Advance(TimeSpan.FromMinutes(54));
        ExpectLoad(row, rootKey);
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        Assert.Equal(2, _referenced.UnwrapCount);
    }

    [Fact]
    public async Task GetProtector_DeploymentRootedEntry_IsNeverValidatedBeforeItsRefresh()
    {
        ExpectCreate();
        using var ring = CreateRing();
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);

        // Past the validation interval of an owner root key but within the refresh interval, no store is read.
        _time.Advance(TimeSpan.FromMinutes(4));
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
    }

    [Theory]
    [InlineData(OwnerRootKeyStates.Unavailable)]
    [InlineData("unknown-state")]
    public async Task GetProtector_OwnerRootKeyNotActive_ThrowsAndRefusesTheOwnerUntilTheRetryInterval(
        string state
    )
    {
        var factory = CreateOwnerRootFactory();
        var row = await CreateOwnerRootedRowAsync(factory);
        ExpectLoad(row, CreateRootKey(state));
        using var ring = CreateOwnerRootRing(factory);

        var exception = await Assert.ThrowsAsync<OwnerRootKeyUnavailableException>(async () =>
            await ring.GetProtectorAsync(Owner, [], CancellationToken.None)
        );
        Assert.Equal(NormalizedOwner, exception.Owner);

        // No store manager is queued, so a read of the store fails the test.
        _time.Advance(TimeSpan.FromSeconds(59));
        await Assert.ThrowsAsync<OwnerRootKeyUnavailableException>(async () =>
            await ring.GetProtectorAsync(Owner, [], CancellationToken.None)
        );

        // After the retry interval the ring tries again and serves the restored key.
        _time.Advance(TimeSpan.FromSeconds(2));
        ExpectLoad(row, CreateRootKey());
        var protector = await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        Assert.Equal(row.KeyId, ReadKeyId(protector.ProtectToString("secret"u8, Context)));
    }

    [Fact]
    public async Task GetProtector_OwnerRootedKeyFailsToUnwrap_ThrowsUnavailableWrappingTheFailure()
    {
        var factory = CreateOwnerRootFactory();
        var row = await CreateOwnerRootedRowAsync(factory);
        _referenced.Revoke(RootKeyReference);
        ExpectLoad(row, CreateRootKey());
        using var ring = CreateOwnerRootRing(factory);

        var exception = await Assert.ThrowsAsync<OwnerRootKeyUnavailableException>(async () =>
            await ring.GetProtectorAsync(Owner, [], CancellationToken.None)
        );

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.IsNotAssignableFrom<CryptographicException>(exception);
    }

    [Fact]
    public async Task GetProtector_OwnerRootedAndARequiredKeyIdHasNoRow_ThrowsOnlyForThatCallAndKeepsTheEntry()
    {
        var factory = CreateOwnerRootFactory();
        var row = await CreateOwnerRootedRowAsync(factory);
        var unknownKeyId = Guid.NewGuid();
        var (store, manager) = EnqueueStoreManager();
        SetupFindActive(store, row);
        SetupFindByKeyId(store, unknownKeyId);
        SetupRootKey(manager, CreateRootKey());
        using var ring = CreateOwnerRootRing(factory);

        var exception = await Assert.ThrowsAsync<CryptographicException>(async () =>
            await ring.GetProtectorAsync(
                Owner,
                [CreateEnvelopeFor(unknownKeyId)],
                CancellationToken.None
            )
        );

        Assert.Contains(unknownKeyId.ToString(), exception.Message);
        Assert.Equal(1, _referenced.UnwrapCount);

        // No refusal is recorded and the entry stays cached: no store manager is queued, so a store read or another
        // unwrap fails the test.
        var protector = await ring.GetProtectorAsync(Owner, [], CancellationToken.None);

        Assert.Equal(row.KeyId, ReadKeyId(protector.ProtectToString("secret"u8, Context)));
        Assert.Equal(1, _referenced.UnwrapCount);
    }

    [Fact]
    public async Task GetProtector_ARevokedKeyAfterTheValidationInterval_FailsClosedEvenWhenTheStateIsActive()
    {
        var factory = CreateOwnerRootFactory();
        var row = await CreateOwnerRootedRowAsync(factory);
        ExpectLoad(row, CreateRootKey());
        using var ring = CreateOwnerRootRing(factory);
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);

        _referenced.Revoke(RootKeyReference);
        _time.Advance(TimeSpan.FromHours(1));
        ExpectLoad(row, CreateRootKey());

        await Assert.ThrowsAsync<OwnerRootKeyUnavailableException>(async () =>
            await ring.GetProtectorAsync(Owner, [], CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetProtector_DueValidationWithThreeRowsUnderOneReference_UnwrapsOnceAndServesAllThree()
    {
        var factory = CreateOwnerRootFactory();
        var row1 = await CreateOwnerRootedRowAsync(factory, version: 1);
        var row2 = await CreateOwnerRootedRowAsync(factory, version: 2);
        var row3 = await CreateOwnerRootedRowAsync(factory, version: 3);
        string[] protectedValues = [CreateEnvelopeFor(row1.KeyId), CreateEnvelopeFor(row2.KeyId)];
        ExpectLoadWithRows(row3, CreateRootKey(), row1, row2);
        using var ring = CreateOwnerRootRing(factory);
        await ring.GetProtectorAsync(Owner, protectedValues, CancellationToken.None);
        Assert.Equal(3, _referenced.UnwrapCount);

        // The active row validates the reference, so the other two rows reuse their material.
        _time.Advance(TimeSpan.FromHours(1));
        ExpectLoadWithRows(row3, CreateRootKey(), row1, row2);
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        Assert.Equal(4, _referenced.UnwrapCount);

        // The entry holds all three keys: no store manager is queued, so a store read fails the test.
        var protector = await ring.GetProtectorAsync(
            Owner,
            protectedValues,
            CancellationToken.None
        );
        Assert.Equal(row3.KeyId, ReadKeyId(protector.ProtectToString("secret"u8, Context)));
        Assert.Equal(4, _referenced.UnwrapCount);
    }

    [Fact]
    public async Task GetProtector_DueValidationWithRowsUnderTwoReferences_UnwrapsOnceForEachReference()
    {
        const string otherReference = "kms://tenant-a/other";
        var factory = CreateOwnerRootFactory();
        var row1 = await CreateOwnerRootedRowAsync(factory, version: 1, otherReference);
        var row2 = await CreateOwnerRootedRowAsync(factory, version: 2, otherReference);
        var row3 = await CreateOwnerRootedRowAsync(factory, version: 3);
        var row4 = await CreateOwnerRootedRowAsync(factory, version: 4);
        string[] protectedValues =
        [
            CreateEnvelopeFor(row1.KeyId),
            CreateEnvelopeFor(row2.KeyId),
            CreateEnvelopeFor(row3.KeyId),
        ];
        ExpectLoadWithRows(row4, CreateRootKey(), row1, row2, row3);
        using var ring = CreateOwnerRootRing(factory);
        await ring.GetProtectorAsync(Owner, protectedValues, CancellationToken.None);
        Assert.Equal(4, _referenced.UnwrapCount);

        // The active row validates its reference and one row validates the other one.
        _time.Advance(TimeSpan.FromHours(1));
        ExpectLoadWithRows(row4, CreateRootKey(), row1, row2, row3);
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        Assert.Equal(6, _referenced.UnwrapCount);

        // The entry holds all four keys: no store manager is queued, so a store read fails the test.
        await ring.GetProtectorAsync(Owner, protectedValues, CancellationToken.None);

        // A reference that no longer unwraps fails the owner closed although the active row's reference works.
        _referenced.Revoke(otherReference);
        _time.Advance(TimeSpan.FromHours(1));
        ExpectLoadWithRows(row4, CreateRootKey(), row1, row2, row3);
        await Assert.ThrowsAsync<OwnerRootKeyUnavailableException>(async () =>
            await ring.GetProtectorAsync(Owner, [], CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetProtector_DueValidationWithRowsUnderTwoAccessIdentities_UnwrapsOnceForEachIdentity()
    {
        const string identityA = "test-identity-a";
        const string identityB = "test-identity-b";
        var wrapper = new IdentityRecordingKeyWrapper(_referenced);
        var factory = CreateOwnerRootFactory(wrapper);
        var row1 = await CreateOwnerRootedRowAsync(factory, version: 1, accessIdentity: identityA);
        var row2 = await CreateOwnerRootedRowAsync(factory, version: 2, accessIdentity: identityB);
        var row3 = await CreateOwnerRootedRowAsync(factory, version: 3, accessIdentity: identityA);
        string[] protectedValues = [CreateEnvelopeFor(row1.KeyId), CreateEnvelopeFor(row2.KeyId)];
        ExpectLoadWithRows(row3, CreateRootKey(accessIdentity: identityA), row1, row2);
        using var ring = CreateOwnerRootRing(factory);
        await ring.GetProtectorAsync(Owner, protectedValues, CancellationToken.None);
        Assert.Equal(3, wrapper.UnwrapAccesses.Count);

        // The active row validates its identity and one row validates the other identity.
        _time.Advance(TimeSpan.FromHours(1));
        ExpectLoadWithRows(row3, CreateRootKey(accessIdentity: identityA), row1, row2);
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
        string[] expectedIdentities = [identityA, identityB];
        var validated = wrapper.UnwrapAccesses.Skip(3).Select(access => access.Identity);
        Assert.Equal(expectedIdentities, validated.Order().ToArray());

        // The entry holds all three keys: no store manager is queued, so a store read fails the test.
        await ring.GetProtectorAsync(Owner, protectedValues, CancellationToken.None);

        // An identity that is revoked fails the owner closed although the active row's identity works.
        wrapper.Refuse(identityB);
        _time.Advance(TimeSpan.FromHours(1));
        ExpectLoadWithRows(row3, CreateRootKey(accessIdentity: identityA), row1, row2);
        await Assert.ThrowsAsync<OwnerRootKeyUnavailableException>(async () =>
            await ring.GetProtectorAsync(Owner, [], CancellationToken.None)
        );
    }

    [Fact]
    public async Task GetProtector_DueValidationOfAKeyWhoseRowExists_LogsNoDroppedKey()
    {
        var factory = CreateOwnerRootFactory();
        var row1 = await CreateOwnerRootedRowAsync(factory, version: 1);
        var row2 = await CreateOwnerRootedRowAsync(factory, version: 2);
        var logger = new RecordingLogger();
        ExpectLoadWithRows(row2, CreateRootKey(), row1);
        using var ring = CreateOwnerRootRing(factory, logger);
        await ring.GetProtectorAsync(
            Owner,
            [CreateEnvelopeFor(row1.KeyId)],
            CancellationToken.None
        );

        _time.Advance(TimeSpan.FromHours(1));
        ExpectLoadWithRows(row2, CreateRootKey(), row1);
        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);

        Assert.DoesNotContain(EventIds.MasterKeyDropped, logger.EventIdValues);
    }

    [Fact]
    public async Task GetProtector_OwnerRootedLoadFailsAfterAnEviction_RecordsNoRefusal()
    {
        var factory = CreateOwnerRootFactory();
        var row = await CreateOwnerRootedRowAsync(factory);
        _referenced.Revoke(RootKeyReference);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var (store, manager) = EnqueueStoreManager();
        store
            .Setup(x => x.FindActiveAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(
                async (SecretOwner _, CancellationToken cancellationToken) =>
                {
                    started.TrySetResult();
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
                    return row;
                }
            )
            .Verifiable();
        SetupRootKey(manager, CreateRootKey());
        using var ring = CreateOwnerRootRing(factory);

        var load = ring.GetProtectorAsync(Owner, [], CancellationToken.None).AsTask();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        ring.Evict(SecretOwner.ForTenant("TENANT-a"));
        release.SetResult();
        await Assert.ThrowsAsync<OwnerRootKeyUnavailableException>(async () =>
            await load.WaitAsync(TimeSpan.FromSeconds(10))
        );

        // The failed load recorded no refusal, so the next call reads the store again and serves the restored key.
        _referenced.Restore(RootKeyReference);
        ExpectLoad(row, CreateRootKey());
        var protector = await ring.GetProtectorAsync(Owner, [], CancellationToken.None);

        Assert.Equal(row.KeyId, ReadKeyId(protector.ProtectToString("secret"u8, Context)));
    }

    [Fact]
    public async Task Evict_AfterAFailure_ClearsTheRefusal()
    {
        var factory = CreateOwnerRootFactory();
        var row = await CreateOwnerRootedRowAsync(factory);
        ExpectLoad(row, CreateRootKey(OwnerRootKeyStates.Unavailable));
        using var ring = CreateOwnerRootRing(factory);
        await Assert.ThrowsAsync<OwnerRootKeyUnavailableException>(async () =>
            await ring.GetProtectorAsync(Owner, [], CancellationToken.None)
        );

        ring.Evict(Owner);
        ExpectLoad(row, CreateRootKey());

        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
    }

    [Fact]
    public async Task EvictAll_AfterAFailure_ClearsTheRefusal()
    {
        var factory = CreateOwnerRootFactory();
        var row = await CreateOwnerRootedRowAsync(factory);
        ExpectLoad(row, CreateRootKey(OwnerRootKeyStates.Unavailable));
        using var ring = CreateOwnerRootRing(factory);
        await Assert.ThrowsAsync<OwnerRootKeyUnavailableException>(async () =>
            await ring.GetProtectorAsync(Owner, [], CancellationToken.None)
        );

        ring.EvictAll();
        ExpectLoad(row, CreateRootKey());

        await ring.GetProtectorAsync(Owner, [], CancellationToken.None);
    }

    [Fact]
    public async Task GetProtector_DeploymentRootedUnwrapFailure_PropagatesUnchanged()
    {
        var wrongProvider = new EphemeralKeyWrapper();
        var catalog = new DefaultKeyWrapperCatalog(
            [wrongProvider],
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { RootKeyProvider = KeyWrapperNames.Ephemeral }
            )
        );
        var otherFactory = new MasterKeyFactory(catalog, _recovery, _time);
        var created = await otherFactory.CreateAsync(
            NormalizedOwner,
            1,
            rootKey: null,
            CancellationToken.None
        );
        created.Key.Dispose();

        // The ring's own ephemeral provider is another instance, so it cannot unwrap the row.
        var (store, _) = EnqueueStoreManager();
        SetupFindActive(store, created.Row);
        using var ring = CreateRing();

        await Assert.ThrowsAsync<CryptographicException>(async () =>
            await ring.GetProtectorAsync(Owner, [], CancellationToken.None)
        );

        wrongProvider.Dispose();
    }

    // Records the identifiers of the events that the ring logs.
    private sealed class RecordingLogger : ILogger<DefaultMasterKeyRing>
    {
        public List<int> EventIdValues { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter
        ) => EventIdValues.Add(eventId.Id);
    }

    #endregion
}
