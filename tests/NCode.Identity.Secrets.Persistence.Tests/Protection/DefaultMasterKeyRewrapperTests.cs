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

    // The old provider wrapped the stored rows, the new one is the configured provider.
    private readonly EphemeralKeyWrapper _oldProvider = new();
    private readonly EphemeralKeyWrapper _newProvider = new();

    public DefaultMasterKeyRewrapperTests()
    {
        _catalog = _mocks.Create<IKeyWrapperCatalog>();
        _storeManagerFactory = _mocks.Create<IStoreManagerFactory>();
        _storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<IStoreManager>(_storeManagers.Dequeue()));
        _catalog.Setup(x => x.Get(KeyWrapperNames.Ephemeral)).Returns(_oldProvider);
        _catalog.Setup(x => x.GetConfigured()).Returns(_newProvider);
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

    private DefaultMasterKeyRewrapper CreateRewrapper(RecoveryKeyWrapper recovery)
    {
        // The rows were created with the old provider, so the creating factory selects it.
        return new DefaultMasterKeyRewrapper(
            new MasterKeyFactory(_catalog.Object, recovery, _time),
            _storeManagerFactory.Object,
            recovery,
            NullLogger<DefaultMasterKeyRewrapper>.Instance
        );
    }

    // Creates a row wrapped under the old provider (and under the recovery key when it is configured).
    private async Task<PersistedMasterKey> CreateRowAsync(
        RecoveryKeyWrapper recoveryAtCreation,
        int version = 1
    )
    {
        var oldCatalog = _mocks.Create<IKeyWrapperCatalog>();
        oldCatalog.Setup(x => x.GetConfigured()).Returns(_oldProvider);
        var creator = new MasterKeyFactory(oldCatalog.Object, recoveryAtCreation, _time);
        var created = await creator.CreateAsync(Owner, version, CancellationToken.None);
        created.Key.Dispose();
        created.Row.ConcurrencyToken = $"token-{version}";
        return created.Row;
    }

    private (Mock<IMasterKeyStore> Store, Mock<IStoreManager> Manager) EnqueueStoreManager()
    {
        var store = _mocks.Create<IMasterKeyStore>();
        var manager = _mocks.Create<IStoreManager>();
        manager.Setup(x => x.GetStore<IMasterKeyStore>()).Returns(store.Object);
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _storeManagers.Enqueue(manager.Object);
        return (store, manager);
    }

    private void SetupList(params PersistedMasterKey[] rows)
    {
        var (store, _) = EnqueueStoreManager();
        store
            .Setup(x => x.ListAsync(Owner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>(rows));
    }

    private void SetupSave(PersistedMasterKey row, Exception? failure = null)
    {
        var (store, manager) = EnqueueStoreManager();
        store
            .Setup(x => x.UpdateAsync(row, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var save = manager.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()));
        if (failure is null)
            save.Returns(ValueTask.CompletedTask);
        else
            save.Throws(failure);
    }

    private static MasterKeyRewrapRequest Request(
        bool recoveryOnly = false,
        bool dryRun = false,
        SecretOwner? owner = null,
        string? pem = null
    ) =>
        new()
        {
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
        SetupList(row);
        SetupSave(row);

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
        Assert.NotEqual(originalRootKeyId, row.RootKeyId);

        // The row now unwraps with the configured provider alone and keeps its key check.
        var verify = _mocks.Create<IKeyWrapperCatalog>();
        verify.Setup(x => x.Get(KeyWrapperNames.Ephemeral)).Returns(_newProvider);
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
        _catalog.Reset();
        _catalog.Setup(x => x.GetConfigured()).Returns(_newProvider);
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
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>([current]));
        SetupSave(current);

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 1, 0, 0), result);
    }

    [Fact]
    public async Task Rewrap_AVanishedRowAfterAConflict_CountsAsExaminedOnly()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery);
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
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>([]));

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 0), result);
    }

    [Fact]
    public async Task Rewrap_ARowDestroyedDuringTheRun_CountsAsExaminedOnly()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery);
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
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>([]));

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 0), result);
    }

    [Fact]
    public async Task Rewrap_AnUpdateThatFailsWhileTheRowExists_CountsTheRowFailed()
    {
        using var recovery = CreateRecovery(configured: false);
        var row = await CreateRowAsync(recovery);
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
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>([row]));

        var result = await CreateRewrapper(recovery).RewrapAsync(Request(), CancellationToken.None);

        Assert.Equal(new MasterKeyRewrapResult(1, 0, 0, 1), result);
    }

    [Fact]
    public async Task Rewrap_WithoutAnOwner_PagesEveryRow()
    {
        using var recovery = CreateRecovery(configured: false);
        var first = await CreateRowAsync(recovery, version: 1);
        var second = await CreateRowAsync(recovery, version: 2);
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
            );
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
            );
        SetupSave(second);

        var result = await CreateRewrapper(recovery)
            .RewrapAsync(
                new MasterKeyRewrapRequest { RecoveryPrivateKeyPem = default },
                CancellationToken.None
            );

        Assert.Equal(new MasterKeyRewrapResult(2, 2, 0, 0), result);
    }

    #endregion
}
