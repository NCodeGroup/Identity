// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.KeyWrapping;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class DefaultMasterKeyRewrapperTests : BaseTests
{
    private static SecretOwner Owner { get; } = SecretOwner.ForTenant("tenant-a").Normalize();

    private static RSA RecoveryPair { get; } = RSA.Create(3072);

    private Mock<IKeyWrapperCatalog> Catalog { get; }
    private Mock<IStoreManagerFactory> StoreManagerFactory { get; }
    private Queue<IStoreManager> StoreManagers { get; } = new();
    private FakeTimeProvider Time { get; } = new();
    private bool StoreManagerFactorySetUp { get; set; }

    // The old provider wrapped the stored rows, the new one is the configured provider.
    private EphemeralKeyWrapper OldProvider { get; } = new();
    private EphemeralKeyWrapper NewProvider { get; } = new();

    public DefaultMasterKeyRewrapperTests()
    {
        Catalog = CreateStrictMock<IKeyWrapperCatalog>();
        StoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            OldProvider.Dispose();
            NewProvider.Dispose();
        }

        base.Dispose(disposing);

        if (disposing)
        {
            Assert.Empty(StoreManagers);
        }
    }

    private static RecoveryKeyWrapper CreateRecovery(bool configured) =>
        configured
            ? RecoveryKeyWrapper.FromPem(RecoveryPair.ExportSubjectPublicKeyInfoPem())
            : new RecoveryKeyWrapper(
                Microsoft.Extensions.Options.Options.Create(new SecretProtectionOptions())
            );

    private void SetupOldProvider() =>
        Catalog.Setup(x => x.Get(KeyWrapperNames.Ephemeral)).Returns(OldProvider).Verifiable();

    private void SetupNewProvider() =>
        Catalog.Setup(x => x.GetConfigured()).Returns(NewProvider).Verifiable();

    private DefaultMasterKeyRewrapper CreateRewrapper(RecoveryKeyWrapper recovery) =>
        new(
            new MasterKeyFactory(Catalog.Object, recovery, Time),
            StoreManagerFactory.Object,
            recovery,
            NullLogger<DefaultMasterKeyRewrapper>.Instance
        );

    // Creates a row wrapped under the old provider (and under the recovery key when it is configured).
    private async ValueTask<PersistedMasterKey> CreateRowAsync(
        RecoveryKeyWrapper recoveryAtCreation,
        int version,
        CancellationToken cancellationToken
    )
    {
        // The row is created with the old provider, which its own factory selects.
        var oldCatalog = CreateStrictMock<IKeyWrapperCatalog>();
        oldCatalog.Setup(x => x.GetConfigured()).Returns(OldProvider).Verifiable();
        var creator = new MasterKeyFactory(oldCatalog.Object, recoveryAtCreation, Time);
        var created = await creator.CreateAsync(Owner, version, rootKey: null, cancellationToken);
        created.Key.Dispose();
        created.Row.ConcurrencyToken = $"token-{version}";
        return created.Row;
    }

    private (Mock<IMasterKeyStore> Store, Mock<IStoreManager> Manager) EnqueueStoreManager(
        int maxRootKeyReads = 1
    )
    {
        if (!StoreManagerFactorySetUp)
        {
            StoreManagerFactorySetUp = true;
            StoreManagerFactory
                .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
                .Returns(() => new ValueTask<IStoreManager>(StoreManagers.Dequeue()))
                .Verifiable();
        }

        var store = CreateStrictMock<IMasterKeyStore>();
        var manager = CreateStrictMock<IStoreManager>();
        manager.Setup(x => x.GetStore<IMasterKeyStore>()).Returns(store.Object).Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();

        // A unit of work that lists or pages rows also reads the root key of each owner on them, which is the one that
        // the test sets. A unit of work that only writes a row never touches this store, and one that is reused for
        // several attempts reads it once per attempt.
        var rootKeyStore = CreateStrictMock<IOwnerRootKeyStore>();
        rootKeyStore
            .Setup(x => x.FindAsync(It.IsAny<SecretOwner>(), It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<PersistedOwnerRootKey?>(OwnerRootKey))
            .Verifiable(Times.Between(0, maxRootKeyReads, Moq.Range.Inclusive));
        manager
            .Setup(x => x.GetStore<IOwnerRootKeyStore>())
            .Returns(rootKeyStore.Object)
            .Verifiable(Times.Between(0, maxRootKeyReads, Moq.Range.Inclusive));

        StoreManagers.Enqueue(manager.Object);
        return (store, manager);
    }

    // The root key that the owner of the tests brings, or none.
    private PersistedOwnerRootKey? OwnerRootKey { get; set; }

    private FakeReferencedKeyWrapper Referenced { get; } = new();

    private PersistedOwnerRootKey CreateOwnerRootKey(string state = OwnerRootKeyStates.Active) =>
        new()
        {
            OwnerType = Owner.OwnerType,
            OwnerId = Owner.OwnerId,
            KeyWrapperName = Referenced.Name,
            KeyReference = "kms://tenant-a/root",
            State = state,
            CreatedWhen = Time.GetUtcNow(),
            StateChangedWhen = Time.GetUtcNow(),
        };

    // Creates a row wrapped under the owner root key, with no recovery copy.
    private async ValueTask<PersistedMasterKey> CreateOwnerRootedRowAsync(
        int version,
        PersistedOwnerRootKey? rootKey,
        CancellationToken cancellationToken
    )
    {
        var creatorCatalog = CreateStrictMock<IKeyWrapperCatalog>();
        creatorCatalog
            .Setup(x => x.GetReferenced(Referenced.Name))
            .Returns(Referenced)
            .Verifiable();
        using var recovery = CreateRecovery(configured: true);
        var created = await new MasterKeyFactory(creatorCatalog.Object, recovery, Time).CreateAsync(
            Owner,
            version,
            rootKey ?? CreateOwnerRootKey(),
            cancellationToken
        );
        created.Key.Dispose();
        created.Row.ConcurrencyToken = $"token-{version}";
        return created.Row;
    }

    private void SetupReferenced() =>
        Catalog.Setup(x => x.GetReferenced(Referenced.Name)).Returns(Referenced).Verifiable();

    private void SetupGetReferenced() =>
        Catalog.Setup(x => x.Get(Referenced.Name)).Returns(Referenced).Verifiable();

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
        var row = await CreateRowAsync(recovery, version: 1, CancellationToken.None);
        var originalRootKeyId = row.RootKeyId;
        SetupOldProvider();
        SetupNewProvider();
        SetupList(row);
        SetupSave(row);

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
        Assert.NotEqual(originalRootKeyId, row.RootKeyId);

        // The row now unwraps with the configured provider alone and keeps its key check.
        var verify = CreateStrictMock<IKeyWrapperCatalog>();
        verify.Setup(x => x.Get(KeyWrapperNames.Ephemeral)).Returns(NewProvider).Verifiable();
        using var key = await new MasterKeyFactory(verify.Object, recovery, Time).UnwrapAsync(
            row,
            CancellationToken.None
        );
        Assert.Equal(row.KeyId, key.KeyId);
    }

    [Fact]
    public async Task Rewrap_WithARecoveryKeyConfigured_ReplacesTheRecoveryCopy()
    {
        using var recovery = CreateRecovery(configured: true);
        var row = await CreateRowAsync(
            CreateRecovery(configured: false),
            version: 1,
            CancellationToken.None
        );
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
        var row = await CreateRowAsync(creatingRecovery, version: 1, CancellationToken.None);
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
        var row = await CreateRowAsync(
            CreateRecovery(configured: false),
            version: 1,
            CancellationToken.None
        );
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
        var row = await CreateRowAsync(recovery, version: 1, CancellationToken.None);

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
        var row = await CreateRowAsync(recovery, version: 1, CancellationToken.None);
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
        var row = await CreateRowAsync(recovery, version: 1, CancellationToken.None);
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
        var good = await CreateRowAsync(recovery, version: 1, CancellationToken.None);
        var bad = await CreateRowAsync(recovery, version: 2, CancellationToken.None);
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
        var row = await CreateRowAsync(recovery, version: 1, CancellationToken.None);
        SetupOldProvider();
        SetupNewProvider();
        SetupList(row);
        SetupSave(row, new StoreConcurrencyException());

        // The row is read again by its key id, then written.
        var current = await CreateRowAsync(recovery, version: 1, CancellationToken.None);
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
        var row = await CreateRowAsync(recovery, version: 1, CancellationToken.None);
        SetupOldProvider();
        SetupNewProvider();
        SetupList(row);

        // Each re-read finds the row as the other writer left it, not as this attempt changed it.
        var reads = new Queue<PersistedMasterKey>([
            await CreateRowAsync(recovery, version: 1, CancellationToken.None),
            await CreateRowAsync(recovery, version: 1, CancellationToken.None),
        ]);
        reads.Enqueue(await CreateRowAsync(recovery, version: 1, CancellationToken.None));

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
        var (findStore, findManager) = EnqueueStoreManager(maxRootKeyReads: 3);
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
            StoreManagers.Enqueue(saveManager.Object);
            StoreManagers.Enqueue(findManager.Object);
        }

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 1), result);
    }

    [Fact]
    public async Task Rewrap_AVanishedRowAfterAConflict_CountsAsExaminedOnly()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery, version: 1, CancellationToken.None);
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
        var row = await CreateRowAsync(recovery, version: 1, CancellationToken.None);
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
        var row = await CreateRowAsync(recovery, version: 1, CancellationToken.None);
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
        var first = await CreateRowAsync(recovery, version: 1, CancellationToken.None);
        var second = await CreateRowAsync(recovery, version: 2, CancellationToken.None);
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
        var row = await CreateRowAsync(recoveryAtCreation, version: 1, CancellationToken.None);
        Assert.NotNull(row.RecoveryWrappedKey);
        OwnerRootKey = CreateOwnerRootKey();
        SetupOldProvider();
        SetupReferenced();
        SetupList(row);
        SetupSave(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(nonconformingOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
        Assert.Equal(Referenced.Name, row.RootKeyProvider);
        Assert.Equal("kms://tenant-a/root", row.RootKeyReference);
        Assert.Null(row.RecoveryKeyId);
        Assert.Null(row.RecoveryWrappedKey);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Rewrap_AConformingOwnerRootedRow_IsExaminedOnly(bool nonconformingOnly)
    {
        var row = await CreateOwnerRootedRowAsync(
            version: 1,
            rootKey: null,
            CancellationToken.None
        );
        var rootWrapped = row.RootWrappedKey;
        OwnerRootKey = CreateOwnerRootKey();
        SetupList(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(nonconformingOnly: nonconformingOnly), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 0), result);
        Assert.Equal(rootWrapped, row.RootWrappedKey);
        Assert.Equal(0, Referenced.UnwrapCount);
    }

    [Fact]
    public async Task Rewrap_DryRunOfAConformingOwnerRootedRow_VerifiesItThroughTheOwnerRootKey()
    {
        var row = await CreateOwnerRootedRowAsync(
            version: 1,
            rootKey: null,
            CancellationToken.None
        );
        OwnerRootKey = CreateOwnerRootKey();
        SetupGetReferenced();
        SetupList(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(dryRun: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 1, 0), result);
        Assert.Equal(1, Referenced.UnwrapCount);
    }

    [Fact]
    public async Task Rewrap_AConflictThatFindsTheOwnerBoundToAnotherKey_LeavesTheRowUnderThatKeyWithoutARecoveryCopy()
    {
        var row = await CreateOwnerRootedRowAsync(
            version: 1,
            rootKey: null,
            CancellationToken.None
        );
        OwnerRootKey = CreateOwnerRootKey(OwnerRootKeyStates.Removing);
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
                OwnerRootKey = keyB;
            })
            .Throws(new StoreConcurrencyException())
            .Verifiable();

        var keyBBinding = CreateOwnerRootKey();
        keyBBinding.KeyReference = "kms://tenant-a/key-b";
        var current = await CreateOwnerRootedRowAsync(
            version: 1,
            rootKey: keyBBinding,
            CancellationToken.None
        );
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
        var row = await CreateOwnerRootedRowAsync(
            version: 1,
            rootKey: null,
            CancellationToken.None
        );
        OwnerRootKey = null;
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
        var row = await CreateOwnerRootedRowAsync(
            version: 1,
            rootKey: null,
            CancellationToken.None
        );
        OwnerRootKey = CreateOwnerRootKey(OwnerRootKeyStates.Removing);
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
        var row = await CreateOwnerRootedRowAsync(
            version: 1,
            rootKey: null,
            CancellationToken.None
        );
        OwnerRootKey = null;
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
        Assert.Equal(1, Referenced.UnwrapCount);
    }

    [Fact]
    public async Task Rewrap_RecoveryOnlyOnAnOwnerRootedRowWithARecoveryCopy_RemovesTheCopyAndKeepsTheRootWrap()
    {
        using var recoveryAtCreation = CreateRecovery(configured: true);
        var row = await CreateOwnerRootedRowAsync(
            version: 1,
            rootKey: null,
            CancellationToken.None
        );
        row.RecoveryKeyId = recoveryAtCreation.RecoveryKeyId;
        row.RecoveryWrappedKey = "copy";
        var rootWrapped = row.RootWrappedKey;
        OwnerRootKey = CreateOwnerRootKey();
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
        var row = await CreateOwnerRootedRowAsync(
            version: 1,
            rootKey: null,
            CancellationToken.None
        );
        OwnerRootKey = CreateOwnerRootKey();
        SetupList(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(recoveryOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 0), result);
    }

    [Fact]
    public async Task Rewrap_RecoveryOnlyOnADeploymentRootedRowOfAnOwnerRootedOwner_KeepsTheRecoveryCopy()
    {
        using var recoveryAtCreation = CreateRecovery(configured: true);
        var row = await CreateRowAsync(recoveryAtCreation, version: 1, CancellationToken.None);
        row.RecoveryWrappedKey = "stale";
        var rootWrapped = row.RootWrappedKey;
        OwnerRootKey = CreateOwnerRootKey();
        SetupOldProvider();
        SetupList(row);
        SetupSave(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(recoveryOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
        Assert.Null(row.RootKeyReference);
        Assert.Equal(recovery.RecoveryKeyId, row.RecoveryKeyId);
        Assert.NotNull(row.RecoveryWrappedKey);
        Assert.NotEqual("stale", row.RecoveryWrappedKey);
        Assert.Equal(rootWrapped, row.RootWrappedKey);
    }

    [Fact]
    public async Task Rewrap_RecoveryOnlyOnADeploymentRootedRowWithoutARecoveryCopyOfAnOwnerRootedOwner_WritesTheRecoveryCopy()
    {
        using var recoveryAtCreation = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recoveryAtCreation, version: 1, CancellationToken.None);
        Assert.Null(row.RecoveryWrappedKey);
        Assert.Null(row.RecoveryKeyId);
        var rootWrapped = row.RootWrappedKey;
        OwnerRootKey = CreateOwnerRootKey();
        SetupOldProvider();
        SetupList(row);
        SetupSave(row);
        using var recovery = CreateRecovery(configured: true);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(recoveryOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
        Assert.Null(row.RootKeyReference);
        Assert.Equal(recovery.RecoveryKeyId, row.RecoveryKeyId);
        Assert.NotNull(row.RecoveryWrappedKey);
        Assert.Equal(rootWrapped, row.RootWrappedKey);
    }

    [Fact]
    public async Task Rewrap_NonconformingOnlyOfAConformingDeploymentRootedRow_UnwrapsNothing()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery, version: 1, CancellationToken.None);
        OwnerRootKey = null;
        SetupList(row);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(nonconformingOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 0), result);
    }

    [Fact]
    public async Task Rewrap_AnOwnerRootKeyThatIsUnavailable_CountsANonconformingRowFailed()
    {
        using var recoveryAtCreation = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recoveryAtCreation, version: 1, CancellationToken.None);
        OwnerRootKey = CreateOwnerRootKey(OwnerRootKeyStates.Unavailable);
        SetupOldProvider();
        SetupList(row);
        using var recovery = CreateRecovery(configured: false);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(Request(nonconformingOnly: true), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 1), result);
    }

    #endregion
}
