// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Secrets.Persistence.DataContracts;
using NCode.Identity.Secrets.Persistence.Options;
using NCode.Persistence.Stores;

namespace NCode.Identity.Secrets.Persistence.Protection;

public class DefaultMasterKeyManagerTests : IDisposable
{
    private static readonly SecretOwner Owner = SecretOwner.ForTenant("Tenant-A");
    private static readonly SecretOwner NormalizedOwner = Owner.Normalize();

    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly Mock<ISecretProtectorProvider> _provider;
    private readonly Mock<IStoreManagerFactory> _storeManagerFactory;
    private readonly Mock<IRecoveryKeyWrapper> _recovery;
    private readonly Mock<IProtectedValueSource> _source;
    private readonly Mock<IProtectedValueSource> _otherSource;
    private readonly Mock<IMasterKeyChangeObserver> _observer;
    private readonly Queue<IStoreManager> _storeManagers = new();
    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero)
    );
    private readonly MasterKeyOptions _keyOptions = new();

    public DefaultMasterKeyManagerTests()
    {
        _provider = _mocks.Create<ISecretProtectorProvider>();
        _storeManagerFactory = _mocks.Create<IStoreManagerFactory>();
        _recovery = _mocks.Create<IRecoveryKeyWrapper>();
        _source = _mocks.Create<IProtectedValueSource>();
        _otherSource = _mocks.Create<IProtectedValueSource>();
        _observer = _mocks.Create<IMasterKeyChangeObserver>();
        _storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<IStoreManager>(_storeManagers.Dequeue()));
    }

    public void Dispose()
    {
        _mocks.Verify();
        Assert.Empty(_storeManagers);
    }

    private DefaultMasterKeyManager CreateManager() =>
        new(
            _provider.Object,
            _storeManagerFactory.Object,
            _recovery.Object,
            [_source.Object, _otherSource.Object],
            [_observer.Object],
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { MasterKeys = _keyOptions }
            ),
            _time,
            NullLogger<DefaultMasterKeyManager>.Instance
        );

    private (Mock<IMasterKeyStore> Store, Mock<IStoreManager> Manager) EnqueueStoreManager()
    {
        var store = _mocks.Create<IMasterKeyStore>();
        var manager = _mocks.Create<IStoreManager>();
        manager.Setup(x => x.GetStore<IMasterKeyStore>()).Returns(store.Object);
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        _storeManagers.Enqueue(manager.Object);
        return (store, manager);
    }

    private static PersistedMasterKey CreateRow(
        int version,
        DateTimeOffset createdWhen,
        DateTimeOffset? retiredWhen = null
    ) =>
        new()
        {
            KeyId = Guid.NewGuid(),
            OwnerType = NormalizedOwner.OwnerType,
            OwnerId = NormalizedOwner.OwnerId,
            Version = version,
            CreatedWhen = createdWhen,
            RetiredWhen = retiredWhen,
            RootKeyProvider = "local",
            RootKeyId = "root",
            RootWrappedKey = "wrapped-secret",
            RecoveryKeyId = "recovery",
            RecoveryWrappedKey = "recovery-wrapped-secret",
            KeyCheck = "check-secret",
            ConcurrencyToken = "token",
        };

    private void SetupList(params PersistedMasterKey[] rows)
    {
        var (store, _) = EnqueueStoreManager();
        store
            .Setup(x => x.ListAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>(rows));
    }

    private void SetupObserverRotated()
    {
        _observer
            .Setup(x =>
                x.RotatedAsync(It.IsAny<MasterKeyRotation>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask);
    }

    // Queues the units of work of a rotation: retire the active row, then read the new active row after the ring
    // created it.
    private void SetupRotation(PersistedMasterKey? active, PersistedMasterKey created)
    {
        var (retireStore, retireManager) = EnqueueStoreManager();
        retireStore
            .Setup(x => x.FindActiveAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PersistedMasterKey?>(active));
        if (active is not null)
        {
            retireStore
                .Setup(x => x.UpdateAsync(active, It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
            retireManager
                .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
        }

        _provider.Setup(x => x.Evict(Owner));
        _provider
            .Setup(x =>
                x.GetProtectorAsync(Owner, It.IsAny<IReadOnlyCollection<string>>(), default)
            )
            .ReturnsAsync(_mocks.Create<ISecretProtector>().Object);

        var (readStore, _) = EnqueueStoreManager();
        readStore
            .Setup(x => x.FindActiveAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PersistedMasterKey?>(created));
    }

    #region GetStatusAsync Tests

    [Fact]
    public async Task GetStatus_MapsEveryRowWithoutMaterial()
    {
        var retired = CreateRow(1, _time.GetUtcNow().AddDays(-400), _time.GetUtcNow().AddDays(-35));
        var active = CreateRow(2, _time.GetUtcNow().AddDays(-35));
        SetupList(active, retired);
        _recovery.SetupGet(x => x.IsConfigured).Returns(true);

        var status = await CreateManager().GetStatusAsync(Owner, CancellationToken.None);

        Assert.Equal(Owner, status.Owner);
        Assert.True(status.RecoveryConfigured);
        Assert.Collection(
            status.Keys,
            key =>
            {
                Assert.Equal(active.KeyId, key.KeyId);
                Assert.Equal(2, key.Version);
                Assert.True(key.IsActive);
                Assert.Equal("local", key.RootKeyProvider);
                Assert.Equal("root", key.RootKeyId);
                Assert.Equal("recovery", key.RecoveryKeyId);
                Assert.Equal(NormalizedOwner, key.Owner);
            },
            key =>
            {
                Assert.Equal(retired.KeyId, key.KeyId);
                Assert.False(key.IsActive);
                Assert.Equal(retired.RetiredWhen, key.RetiredWhen);
            }
        );
    }

    #endregion

    #region RotateAsync Tests

    [Fact]
    public async Task Rotate_RetiresTheActiveRowThenReturnsTheNewActiveRow()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        var created = CreateRow(2, _time.GetUtcNow());
        SetupRotation(active, created);
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Equal(_time.GetUtcNow(), active.RetiredWhen);
        Assert.Equal(created.KeyId, rotation.ActiveKey.KeyId);
        Assert.Equal(2, rotation.ActiveKey.Version);
        Assert.Equal(active.KeyId, rotation.RetiredKey?.KeyId);
        Assert.False(rotation.RetiredKey?.IsActive);
        _observer.Verify(x => x.RotatedAsync(rotation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rotate_WithoutAnActiveRow_CreatesTheFirstKeyAndReportsNoRetiredKey()
    {
        var created = CreateRow(1, _time.GetUtcNow());
        SetupRotation(null, created);
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Null(rotation.RetiredKey);
        Assert.Equal(created.KeyId, rotation.ActiveKey.KeyId);
    }

    [Fact]
    public async Task Rotate_WhenAnotherRotationCommittedFirst_PropagatesAndNotifiesNobody()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        var (store, manager) = EnqueueStoreManager();
        store
            .Setup(x => x.FindActiveAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PersistedMasterKey?>(active));
        store
            .Setup(x => x.UpdateAsync(active, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Throws(new StoreConcurrencyException());

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Rotate_WhenAnObserverThrows_StillReturnsTheRotation()
    {
        SetupRotation(
            CreateRow(1, _time.GetUtcNow().AddDays(-400)),
            CreateRow(2, _time.GetUtcNow())
        );
        _observer
            .Setup(x =>
                x.RotatedAsync(It.IsAny<MasterKeyRotation>(), It.IsAny<CancellationToken>())
            )
            .Throws(new InvalidOperationException("boom"));

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Equal(2, rotation.ActiveKey.Version);
    }

    #endregion

    #region RewrapAsync Tests

    [Fact]
    public async Task Rewrap_AsksEverySourceForEveryRetiredKey()
    {
        var retiredOld = CreateRow(
            1,
            _time.GetUtcNow().AddDays(-800),
            _time.GetUtcNow().AddDays(-400)
        );
        var retiredNew = CreateRow(
            2,
            _time.GetUtcNow().AddDays(-400),
            _time.GetUtcNow().AddDays(-1)
        );
        var active = CreateRow(3, _time.GetUtcNow().AddDays(-1));
        _provider.Setup(x => x.Evict(Owner));
        SetupList(active, retiredNew, retiredOld);
        _source
            .Setup(x => x.RewrapAsync(Owner, retiredNew.KeyId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<int>(2));
        _source
            .Setup(x => x.RewrapAsync(Owner, retiredOld.KeyId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<int>(1));
        _otherSource
            .Setup(x => x.RewrapAsync(Owner, retiredNew.KeyId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<int>(0));
        _otherSource
            .Setup(x => x.RewrapAsync(Owner, retiredOld.KeyId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<int>(4));

        var count = await CreateManager().RewrapAsync(Owner, CancellationToken.None);

        Assert.Equal(7, count);
    }

    #endregion

    #region DestroyRetiredAsync Tests

    private void SetupDestroy(PersistedMasterKey row, bool removed = true)
    {
        var (store, manager) = EnqueueStoreManager();
        store
            .Setup(x => x.RemoveAsync(row, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(removed));
        if (removed)
        {
            manager
                .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
        }
    }

    private void SetupUnreferenced(PersistedMasterKey row)
    {
        _source
            .Setup(x => x.ReferencesAsync(Owner, row.KeyId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(false));
        _otherSource
            .Setup(x => x.ReferencesAsync(Owner, row.KeyId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(false));
    }

    [Fact]
    public async Task Destroy_RemovesAPastGraceUnreferencedKeyAndNotifies()
    {
        var retired = CreateRow(1, _time.GetUtcNow().AddDays(-400), _time.GetUtcNow().AddDays(-31));
        SetupList(retired);
        SetupUnreferenced(retired);
        SetupDestroy(retired);
        _provider.Setup(x => x.Evict(Owner));
        _observer
            .Setup(x => x.DestroyedAsync(It.IsAny<MasterKeyInfo>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Equal(retired.KeyId, Assert.Single(destroyed).KeyId);
        _observer.Verify(
            x => x.DestroyedAsync(destroyed[0], It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    public async Task Destroy_KeepsAKeyInsideTheGrace()
    {
        var retired = CreateRow(1, _time.GetUtcNow().AddDays(-400), _time.GetUtcNow().AddDays(-29));
        SetupList(retired);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Empty(destroyed);
    }

    [Fact]
    public async Task Destroy_KeepsAKeyThatASourceReferences()
    {
        var retired = CreateRow(1, _time.GetUtcNow().AddDays(-400), _time.GetUtcNow().AddDays(-31));
        SetupList(retired);
        _source
            .Setup(x => x.ReferencesAsync(Owner, retired.KeyId, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(true));

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Empty(destroyed);
    }

    [Fact]
    public async Task Destroy_KeepsTheActiveKey()
    {
        SetupList(CreateRow(1, _time.GetUtcNow().AddDays(-400)));

        Assert.Empty(await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None));
    }

    [Fact]
    public async Task Destroy_WhenDestructionIsOff_ReadsNothing()
    {
        _keyOptions.DestroyRetired = false;

        Assert.Empty(await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None));
    }

    #endregion
}
