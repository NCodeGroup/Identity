// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Persistence.Stores;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class DefaultMasterKeyRewrapperTests : IDisposable
{
    private static readonly SecretOwner Owner = SecretOwner.ForTenant("tenant-a").Normalize();

    private static readonly RSA RecoveryPair = RSA.Create(3072);

    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly Mock<IKeyWrapperCatalog> _catalog;
    private readonly Mock<IStoreManagerFactory> _storeManagerFactory;
    private readonly Queue<IStoreManager> _storeManagers = new();
    private readonly FakeTimeProvider _time = new();
    private bool _storeManagerFactorySetUp;

    // The old provider wrapped the stored rows, the new one is the configured provider.
    private readonly EphemeralKeyWrapper _oldProvider = new();
    private readonly EphemeralKeyWrapper _newProvider = new();

    public DefaultMasterKeyRewrapperTests()
    {
        _catalog = _mocks.Create<IKeyWrapperCatalog>();
        _storeManagerFactory = _mocks.Create<IStoreManagerFactory>();
    }

    public void Dispose()
    {
        _oldProvider.Dispose();
        _newProvider.Dispose();
        _mocks.Verify();
        Assert.Empty(_storeManagers);
    }

    private static RecoveryKeyWrapper CreateRecovery(bool configured) =>
        configured
            ? RecoveryKeyWrapper.FromPem(RecoveryPair.ExportSubjectPublicKeyInfoPem())
            : new RecoveryKeyWrapper(
                Microsoft.Extensions.Options.Options.Create(new SecretProtectionOptions())
            );

    private void SetupOldProvider() =>
        _catalog.Setup(x => x.Get(KeyWrapperNames.Ephemeral)).Returns(_oldProvider).Verifiable();

    private void SetupNewProvider() =>
        _catalog.Setup(x => x.GetConfigured()).Returns(_newProvider).Verifiable();

    private DefaultMasterKeyRewrapper CreateRewrapper(RecoveryKeyWrapper recovery) =>
        new(
            new MasterKeyFactory(_catalog.Object, recovery, _time),
            _storeManagerFactory.Object,
            recovery,
            NullLogger<DefaultMasterKeyRewrapper>.Instance
        );

    // Creates a row wrapped under the old provider (and under the recovery key when it is configured).
    private async Task<PersistedMasterKey> CreateRowAsync(
        RecoveryKeyWrapper recoveryAtCreation,
        int version = 1
    )
    {
        // The row is created with the old provider, which its own factory selects.
        var oldCatalog = _mocks.Create<IKeyWrapperCatalog>();
        oldCatalog.Setup(x => x.GetConfigured()).Returns(_oldProvider).Verifiable();
        var creator = new MasterKeyFactory(oldCatalog.Object, recoveryAtCreation, _time);
        var created = await creator.CreateAsync(
            Owner,
            version,
            rootKey: null,
            CancellationToken.None
        );
        created.Key.Dispose();
        created.Row.ConcurrencyToken = $"token-{version}";
        return created.Row;
    }

    private (Mock<IMasterKeyStore> Store, Mock<IStoreManager> Manager) EnqueueStoreManager()
    {
        if (!_storeManagerFactorySetUp)
        {
            _storeManagerFactorySetUp = true;
            _storeManagerFactory
                .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
                .Returns(() => new ValueTask<IStoreManager>(_storeManagers.Dequeue()))
                .Verifiable();
        }

        var store = _mocks.Create<IMasterKeyStore>();
        var manager = _mocks.Create<IStoreManager>();
        manager.Setup(x => x.GetStore<IMasterKeyStore>()).Returns(store.Object).Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        // A unit of work that lists or pages rows also reads the root key of each owner on them, which is the one that
        // the test sets. The units of work that only write or re-read a row never touch this store.
        var rootKeyStore = _mocks.Create<IOwnerRootKeyStore>();
        rootKeyStore
            .Setup(x => x.FindAsync(It.IsAny<SecretOwner>(), It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<PersistedOwnerRootKey?>(_ownerRootKey));
        manager.Setup(x => x.GetStore<IOwnerRootKeyStore>()).Returns(rootKeyStore.Object);

        _storeManagers.Enqueue(manager.Object);
        return (store, manager);
    }

    // The root key that the owner of the tests brings, or none.
    private PersistedOwnerRootKey? _ownerRootKey;

    private readonly FakeReferencedKeyWrapper _referenced = new();

    private PersistedOwnerRootKey CreateOwnerRootKey(string state = OwnerRootKeyStates.Active) =>
        new()
        {
            OwnerType = Owner.OwnerType,
            OwnerId = Owner.OwnerId,
            KeyWrapperName = _referenced.Name,
            KeyReference = "kms://tenant-a/root",
            State = state,
            CreatedWhen = _time.GetUtcNow(),
            StateChangedWhen = _time.GetUtcNow(),
        };

    // Creates a row wrapped under the owner root key, with no recovery copy.
    private async Task<PersistedMasterKey> CreateOwnerRootedRowAsync(
        int version = 1,
        PersistedOwnerRootKey? rootKey = null
    )
    {
        var creatorCatalog = _mocks.Create<IKeyWrapperCatalog>();
        creatorCatalog.Setup(x => x.GetReferenced(_referenced.Name)).Returns(_referenced);
        using var recovery = CreateRecovery(configured: true);
        var created = await new MasterKeyFactory(
            creatorCatalog.Object,
            recovery,
            _time
        ).CreateAsync(Owner, version, rootKey ?? CreateOwnerRootKey(), CancellationToken.None);
        created.Key.Dispose();
        created.Row.ConcurrencyToken = $"token-{version}";
        return created.Row;
    }

    private void SetupReferenced() =>
        _catalog.Setup(x => x.GetReferenced(_referenced.Name)).Returns(_referenced);

    private void SetupGetReferenced() =>
        _catalog.Setup(x => x.Get(_referenced.Name)).Returns(_referenced);

    private void SetupList(params PersistedMasterKey[] rows)
    {
        var (store, _) = EnqueueStoreManager();
        store
            .Setup(x => x.ListAsync(Owner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>(rows))
            .Verifiable();
    }

    private void SetupSave(PersistedMasterKey row, Exception? failure = null)
    {
        var (store, manager) = EnqueueStoreManager();
        store
            .Setup(x => x.UpdateAsync(row, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        var save = manager.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()));
        if (failure is null)
            save.Returns(ValueTask.CompletedTask).Verifiable();
        else
            save.Throws(failure).Verifiable();
    }

    private static MasterKeyRewrapRequest Request(
        bool recoveryOnly = false,
        bool dryRun = false,
        SecretOwner? owner = null,
        string? pem = null,
        bool nonconformingOnly = false
    ) =>
        new()
        {
            NonconformingOnly = nonconformingOnly,
            RecoveryOnly = recoveryOnly,
            DryRun = dryRun,
            Owner = owner ?? Owner,
            RecoveryPrivateKeyPem = pem.AsMemory(),
        };

    #region RewrapAsync Tests

    [Fact]
    public async Task Rewrap_MovesTheRootWrapToTheConfiguredProviderAndKeepsTheMaterial()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery);
        var originalRootKeyId = row.RootKeyId;
        SetupOldProvider();
        SetupNewProvider();
        SetupList(row);
        SetupSave(row);

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
        Assert.NotEqual(originalRootKeyId, row.RootKeyId);

        // The row now unwraps with the configured provider alone and keeps its key check.
        var verify = _mocks.Create<IKeyWrapperCatalog>();
        verify.Setup(x => x.Get(KeyWrapperNames.Ephemeral)).Returns(_newProvider).Verifiable();
        using var key = await new MasterKeyFactory(verify.Object, recovery, _time).UnwrapAsync(
            row,
            CancellationToken.None
        );
        Assert.Equal(row.KeyId, key.KeyId);
    }

    [Fact]
    public async Task Rewrap_WithARecoveryKeyConfigured_ReplacesTheRecoveryCopy()
    {
        using var recovery = CreateRecovery(configured: true);
        var row = await CreateRowAsync(CreateRecovery(configured: false));
        Assert.Null(row.RecoveryWrappedKey);
        SetupOldProvider();
        SetupNewProvider();
        SetupList(row);
        SetupSave(row);

        await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(recovery.RecoveryKeyId, row.RecoveryKeyId);
        Assert.NotNull(row.RecoveryWrappedKey);
    }

    [Fact]
    public async Task Rewrap_WithoutARecoveryKey_LeavesTheExistingRecoveryCopy()
    {
        using var creatingRecovery = CreateRecovery(configured: true);
        var row = await CreateRowAsync(creatingRecovery);
        var copy = row.RecoveryWrappedKey;
        using var recovery = CreateRecovery(configured: false);
        SetupOldProvider();
        SetupNewProvider();
        SetupList(row);
        SetupSave(row);

        await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(copy, row.RecoveryWrappedKey);
    }

    [Fact]
    public async Task Rewrap_RecoveryOnly_ReplacesTheRecoveryCopyAndKeepsTheRootWrap()
    {
        using var recovery = CreateRecovery(configured: true);
        var row = await CreateRowAsync(CreateRecovery(configured: false));
        var rootWrapped = row.RootWrappedKey;
        SetupOldProvider();
        SetupList(row);
        SetupSave(row);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(recoveryOnly: true), CancellationToken.None);

        Assert.Equal(1, result.Rewrapped);
        Assert.Equal(rootWrapped, row.RootWrappedKey);
        Assert.NotNull(row.RecoveryWrappedKey);
    }

    [Fact]
    public async Task Rewrap_RecoveryOnlyWithoutARecoveryKey_ThrowsBeforeReadingAnyRow()
    {
        using var recovery = CreateRecovery(configured: false);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateRewrapper(recovery)
                .RewrapAsync(Request(recoveryOnly: true), CancellationToken.None)
        );
    }

    [Fact]
    public async Task Rewrap_WithTheRecoveryPrivateKey_UnwrapsFromTheRecoveryCopy()
    {
        using var recovery = CreateRecovery(configured: true);
        var row = await CreateRowAsync(recovery);

        // The root provider that wrapped the row is unavailable: only the recovery copy unwraps it.
        SetupNewProvider();
        SetupList(row);
        SetupSave(row);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(
                Request(pem: RecoveryPair.ExportPkcs8PrivateKeyPem()),
                CancellationToken.None
            );

        Assert.Equal(1, result.Rewrapped);
    }

    [Fact]
    public async Task Rewrap_WithTheRecoveryPrivateKeyOfAnotherKey_CountsTheRowFailed()
    {
        using var recovery = CreateRecovery(configured: true);
        var row = await CreateRowAsync(recovery);
        SetupList(row);
        using var other = RSA.Create(3072);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(pem: other.ExportPkcs8PrivateKeyPem()), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 1), result);
    }

    [Fact]
    public async Task Rewrap_DryRun_WritesNothingAndCountsVerified()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery);
        var rootWrapped = row.RootWrappedKey;
        SetupOldProvider();
        SetupList(row);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(dryRun: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 1, 0), result);
        Assert.Equal(rootWrapped, row.RootWrappedKey);
    }

    [Fact]
    public async Task Rewrap_AKeyCheckMismatch_CountsTheRowFailed()
    {
        using var recovery = CreateRecovery(configured: false);
        var good = await CreateRowAsync(recovery, version: 1);
        var bad = await CreateRowAsync(recovery, version: 2);
        var tampered = new PersistedMasterKey
        {
            KeyId = bad.KeyId,
            OwnerType = bad.OwnerType,
            OwnerId = bad.OwnerId,
            Version = bad.Version,
            CreatedWhen = bad.CreatedWhen,
            RootKeyProvider = bad.RootKeyProvider,
            RootKeyId = bad.RootKeyId,
            RootWrappedKey = bad.RootWrappedKey,
            KeyCheck = good.KeyCheck,
        };
        SetupOldProvider();
        SetupNewProvider();
        SetupList(tampered, good);
        SetupSave(good);

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(2, 1, 0, 1), result);
    }

    [Fact]
    public async Task Rewrap_AConflict_ReadsTheRowAgainAndRetries()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery);
        SetupOldProvider();
        SetupNewProvider();
        SetupList(row);
        SetupSave(row, new StoreConcurrencyException());

        // The row is read again by its key id, then written.
        var current = await CreateRowAsync(recovery);
        var (store, _) = EnqueueStoreManager();
        store
            .Setup(x =>
                x.FindByKeyIdsAsync(
                    Owner,
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(row.KeyId)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>([current]))
            .Verifiable();
        SetupSave(current);

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
    }

    [Fact]
    public async Task Rewrap_ConflictsOnEveryAttempt_StopsAfterThreeAttemptsAndCountsTheRowFailed()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery);
        SetupOldProvider();
        SetupNewProvider();
        SetupList(row);

        // Each re-read finds the row as the other writer left it, not as this attempt changed it.
        var reads = new Queue<PersistedMasterKey>([
            await CreateRowAsync(recovery),
            await CreateRowAsync(recovery),
        ]);
        reads.Enqueue(await CreateRowAsync(recovery));

        var (saveStore, saveManager) = EnqueueStoreManager();
        saveStore
            .Setup(x =>
                x.UpdateAsync(It.IsAny<PersistedMasterKey>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(3));
        saveManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Throws(new StoreConcurrencyException())
            .Verifiable(Times.Exactly(3));
        var (findStore, findManager) = EnqueueStoreManager();
        findStore
            .Setup(x =>
                x.FindByKeyIdsAsync(
                    Owner,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(() => new ValueTask<IReadOnlyList<PersistedMasterKey>>([reads.Dequeue()]))
            .Verifiable(Times.Exactly(3));

        // The same two units of work serve the second and the third attempt.
        for (var attempt = 2; attempt <= 3; attempt++)
        {
            _storeManagers.Enqueue(saveManager.Object);
            _storeManagers.Enqueue(findManager.Object);
        }

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 1), result);
    }

    [Fact]
    public async Task Rewrap_AVanishedRowAfterAConflict_CountsAsExaminedOnly()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery);
        SetupOldProvider();
        SetupNewProvider();
        SetupList(row);
        SetupSave(row, new StoreConcurrencyException());
        var (store, _) = EnqueueStoreManager();
        store
            .Setup(x =>
                x.FindByKeyIdsAsync(
                    Owner,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>([]))
            .Verifiable();

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 0), result);
    }

    [Fact]
    public async Task Rewrap_ARowDestroyedDuringTheRun_CountsAsExaminedOnly()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery);
        SetupOldProvider();
        SetupNewProvider();
        SetupList(row);
        SetupSave(row, new InvalidOperationException("The row no longer exists."));
        var (store, _) = EnqueueStoreManager();
        store
            .Setup(x =>
                x.FindByKeyIdsAsync(
                    Owner,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>([]))
            .Verifiable();

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 0), result);
    }

    [Fact]
    public async Task Rewrap_AnUpdateThatFailsWhileTheRowExists_CountsTheRowFailed()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery);
        SetupOldProvider();
        SetupNewProvider();
        SetupList(row);
        SetupSave(row, new InvalidOperationException("The update failed."));
        var (store, _) = EnqueueStoreManager();
        store
            .Setup(x =>
                x.FindByKeyIdsAsync(
                    Owner,
                    It.IsAny<IReadOnlyCollection<Guid>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>([row]))
            .Verifiable();

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 1), result);
    }

    [Fact]
    public async Task Rewrap_WithoutAnOwner_PagesEveryRow()
    {
        using var recovery = CreateRecovery(configured: false);
        var first = await CreateRowAsync(recovery, version: 1);
        var second = await CreateRowAsync(recovery, version: 2);
        SetupOldProvider();
        SetupNewProvider();
        var (store1, _) = EnqueueStoreManager();
        store1
            .Setup(x =>
                x.GetPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == null),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PagedResult<PersistedMasterKey>>(
                    new PagedResult<PersistedMasterKey> { Items = [first], NextCursor = "next" }
                )
            )
            .Verifiable();
        SetupSave(first);
        var (store2, _) = EnqueueStoreManager();
        store2
            .Setup(x =>
                x.GetPageAsync(
                    It.Is<PageRequest>(page => page.Cursor == "next"),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<PagedResult<PersistedMasterKey>>(
                    new PagedResult<PersistedMasterKey> { Items = [second], NextCursor = null }
                )
            )
            .Verifiable();
        SetupSave(second);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(
                new MasterKeyRewrapRequest { RecoveryPrivateKeyPem = default },
                CancellationToken.None
            );

        Assert.Equal(new MasterKeyRewrapResult(2, 2, 0, 0), result);
    }

    #endregion

    #region Owner Root Key Tests

    [Fact]
    public async Task Rewrap_ADeploymentRootedRowOfABoundOwner_MovesUnderTheOwnerRootKeyAndLosesItsRecoveryCopy()
    {
        using var recoveryAtCreation = CreateRecovery(configured: true);
        var row = await CreateRowAsync(recoveryAtCreation);
        Assert.NotNull(row.RecoveryWrappedKey);
        _ownerRootKey = CreateOwnerRootKey();
        SetupOldProvider();
        SetupReferenced();
        SetupList(row);
        SetupSave(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(nonconformingOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
        Assert.Equal(_referenced.Name, row.RootKeyProvider);
        Assert.Equal("kms://tenant-a/root", row.RootKeyReference);
        Assert.Null(row.RecoveryKeyId);
        Assert.Null(row.RecoveryWrappedKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rewrap_AConformingOwnerRootedRow_IsExaminedOnly(bool nonconformingOnly)
    {
        var row = await CreateOwnerRootedRowAsync();
        var rootWrapped = row.RootWrappedKey;
        _ownerRootKey = CreateOwnerRootKey();
        SetupList(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(nonconformingOnly: nonconformingOnly), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 0), result);
        Assert.Equal(rootWrapped, row.RootWrappedKey);
        Assert.Equal(0, _referenced.UnwrapCount);
    }

    [Fact]
    public async Task Rewrap_DryRunOfAConformingOwnerRootedRow_VerifiesItThroughTheOwnerRootKey()
    {
        var row = await CreateOwnerRootedRowAsync();
        _ownerRootKey = CreateOwnerRootKey();
        SetupGetReferenced();
        SetupList(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(dryRun: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 1, 0), result);
        Assert.Equal(1, _referenced.UnwrapCount);
    }

    [Fact]
    public async Task Rewrap_AConflictThatFindsTheOwnerBoundToAnotherKey_LeavesTheRowUnderThatKeyWithoutARecoveryCopy()
    {
        var row = await CreateOwnerRootedRowAsync();
        _ownerRootKey = CreateOwnerRootKey(OwnerRootKeyStates.Removing);
        SetupGetReferenced();
        SetupNewProvider();
        SetupList(row);

        // The save conflicts because another writer bound the owner to key B and moved the row under it.
        var (saveStore, saveManager) = EnqueueStoreManager();
        saveStore
            .Setup(x => x.UpdateAsync(row, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        saveManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                var keyB = CreateOwnerRootKey();
                keyB.KeyReference = "kms://tenant-a/key-b";
                _ownerRootKey = keyB;
            })
            .Throws(new StoreConcurrencyException())
            .Verifiable();

        var keyBBinding = CreateOwnerRootKey();
        keyBBinding.KeyReference = "kms://tenant-a/key-b";
        var current = await CreateOwnerRootedRowAsync(rootKey: keyBBinding);
        var (findStore, _) = EnqueueStoreManager();
        findStore
            .Setup(x =>
                x.FindByKeyIdsAsync(
                    Owner,
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Contains(row.KeyId)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>([current]))
            .Verifiable();
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 0), result);
        Assert.Equal("kms://tenant-a/key-b", current.RootKeyReference);
        Assert.Null(current.RecoveryKeyId);
        Assert.Null(current.RecoveryWrappedKey);
    }

    [Fact]
    public async Task Rewrap_ARowOfAnUnboundOwner_MovesUnderTheDeploymentRootWithARecoveryCopy()
    {
        var row = await CreateOwnerRootedRowAsync();
        _ownerRootKey = null;
        SetupGetReferenced();
        SetupNewProvider();
        SetupList(row);
        SetupSave(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(nonconformingOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
        Assert.Equal(KeyWrapperNames.Ephemeral, row.RootKeyProvider);
        Assert.Null(row.RootKeyReference);
        Assert.Equal(recovery.RecoveryKeyId, row.RecoveryKeyId);
        Assert.NotNull(row.RecoveryWrappedKey);
    }

    [Fact]
    public async Task Rewrap_ARowOfAnOwnerWhoseRootKeyIsBeingRemoved_MovesUnderTheDeploymentRoot()
    {
        var row = await CreateOwnerRootedRowAsync();
        _ownerRootKey = CreateOwnerRootKey(OwnerRootKeyStates.Removing);
        SetupGetReferenced();
        SetupNewProvider();
        SetupList(row);
        SetupSave(row);
        using var recovery = CreateRecovery(configured: false);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(nonconformingOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
        Assert.Null(row.RootKeyReference);
    }

    [Fact]
    public async Task Rewrap_WithTheRecoveryPrivateKey_UnwrapsAnOwnerRootedRowThroughItsOwnKey()
    {
        var row = await CreateOwnerRootedRowAsync();
        _ownerRootKey = null;
        SetupGetReferenced();
        SetupNewProvider();
        SetupList(row);
        SetupSave(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(
                Request(pem: RecoveryPair.ExportPkcs8PrivateKeyPem()),
                CancellationToken.None
            );

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
        Assert.Equal(1, _referenced.UnwrapCount);
    }

    [Fact]
    public async Task Rewrap_RecoveryOnlyOnAnOwnerRootedRowWithARecoveryCopy_RemovesTheCopyAndKeepsTheRootWrap()
    {
        using var recoveryAtCreation = CreateRecovery(configured: true);
        var row = await CreateOwnerRootedRowAsync();
        row.RecoveryKeyId = recoveryAtCreation.RecoveryKeyId;
        row.RecoveryWrappedKey = "copy";
        var rootWrapped = row.RootWrappedKey;
        _ownerRootKey = CreateOwnerRootKey();
        SetupGetReferenced();
        SetupList(row);
        SetupSave(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(recoveryOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
        Assert.Null(row.RecoveryWrappedKey);
        Assert.Null(row.RecoveryKeyId);
        Assert.Equal(rootWrapped, row.RootWrappedKey);
    }

    [Fact]
    public async Task Rewrap_RecoveryOnlyOnAnOwnerRootedRowWithoutARecoveryCopy_IsExaminedOnly()
    {
        var row = await CreateOwnerRootedRowAsync();
        _ownerRootKey = CreateOwnerRootKey();
        SetupList(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(recoveryOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 0), result);
    }

    [Fact]
    public async Task Rewrap_NonconformingOnlyOfAConformingDeploymentRootedRow_UnwrapsNothing()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery);
        _ownerRootKey = null;
        SetupList(row);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(nonconformingOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 0), result);
    }

    [Fact]
    public async Task Rewrap_AnOwnerRootKeyThatIsUnavailable_CountsANonconformingRowFailed()
    {
        using var recoveryAtCreation = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recoveryAtCreation);
        _ownerRootKey = CreateOwnerRootKey(OwnerRootKeyStates.Unavailable);
        SetupOldProvider();
        SetupList(row);
        using var recovery = CreateRecovery(configured: false);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(nonconformingOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 1), result);
    }

    #endregion
}
