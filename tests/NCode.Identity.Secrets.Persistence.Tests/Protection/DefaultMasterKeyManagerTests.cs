// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Secrets.KeyWrapping;
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
    private readonly Mock<IKeyWrapperCatalog> _catalog;
    private readonly EphemeralKeyWrapper _rootKey = new();
    private readonly Mock<IProtectedValueSource> _source;
    private readonly Mock<IProtectedValueSource> _otherSource;
    private readonly Mock<IMasterKeyChangeObserver> _observer;
    private readonly Queue<IStoreManager> _storeManagers = new();
    private readonly FakeTimeProvider _time = new(
        new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero)
    );
    private readonly MasterKeyOptions _keyOptions = new();
    private bool _storeManagerFactorySetUp;

    public DefaultMasterKeyManagerTests()
    {
        _provider = _mocks.Create<ISecretProtectorProvider>();
        _storeManagerFactory = _mocks.Create<IStoreManagerFactory>();
        _recovery = _mocks.Create<IRecoveryKeyWrapper>();
        _catalog = _mocks.Create<IKeyWrapperCatalog>();
        _source = _mocks.Create<IProtectedValueSource>();
        _otherSource = _mocks.Create<IProtectedValueSource>();
        _observer = _mocks.Create<IMasterKeyChangeObserver>();
    }

    public void Dispose()
    {
        _rootKey.Dispose();
        _mocks.Verify();
        Assert.Empty(_storeManagers);
    }

    private DefaultMasterKeyManager CreateManager() =>
        new(
            _provider.Object,
            _storeManagerFactory.Object,
            _recovery.Object,
            new MasterKeyFactory(_catalog.Object, _recovery.Object, _time),
            [_source.Object, _otherSource.Object],
            [_observer.Object],
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { MasterKeys = _keyOptions }
            ),
            _time,
            NullLogger<DefaultMasterKeyManager>.Instance
        );

    private void SetupRootKey() =>
        _catalog.Setup(x => x.GetConfigured()).Returns(_rootKey).Verifiable();

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

    // Queues a unit of work that lists the rows. A rotation and a status read also read the owner's root key, which is
    // none here; the other operations do not read it, so its setup is not verified.
    private void SetupList(params PersistedMasterKey[] rows) => SetupListWithRootKey(null, rows);

    private void SetupListWithRootKey(
        PersistedOwnerRootKey? rootKey,
        params PersistedMasterKey[] rows
    )
    {
        var (store, manager) = EnqueueStoreManager();
        store
            .Setup(x => x.ListAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>(rows))
            .Verifiable();

        var rootKeyStore = _mocks.Create<IOwnerRootKeyStore>();
        rootKeyStore
            .Setup(x => x.FindAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PersistedOwnerRootKey?>(rootKey));
        manager.Setup(x => x.GetStore<IOwnerRootKeyStore>()).Returns(rootKeyStore.Object);
    }

    private void SetupObserverRotated()
    {
        _observer
            .Setup(x =>
                x.RotatedAsync(It.IsAny<MasterKeyRotation>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
    }

    private void SetupRecoveryNotConfigured() =>
        _recovery.SetupGet(x => x.IsConfigured).Returns(false).Verifiable();

    // Queues the unit of work that retires the active row.
    private void SetupRetire(PersistedMasterKey active, Action? onSave = null)
    {
        var (store, manager) = EnqueueStoreManager();
        store
            .Setup(x => x.UpdateAsync(active, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                onSave?.Invoke();
                return ValueTask.CompletedTask;
            })
            .Verifiable();
    }

    // Queues the unit of work that stores the successor; the saved row and the tokens of the add and the save are
    // captured.
    private (List<PersistedMasterKey> Added, List<bool> TokensCancelled) SetupInsert(
        Exception? saveFailure = null,
        Action<CancellationToken>? onSave = null
    )
    {
        var added = new List<PersistedMasterKey>();
        var cancelled = new List<bool>();
        var (store, manager) = EnqueueStoreManager();
        store
            .Setup(x => x.AddAsync(It.IsAny<PersistedMasterKey>(), It.IsAny<CancellationToken>()))
            .Returns(
                (PersistedMasterKey row, CancellationToken token) =>
                {
                    added.Add(row);
                    cancelled.Add(token.IsCancellationRequested);
                    return ValueTask.CompletedTask;
                }
            )
            .Verifiable();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(
                (CancellationToken token) =>
                {
                    onSave?.Invoke(token);
                    cancelled.Add(token.IsCancellationRequested);
                    return saveFailure is null
                        ? ValueTask.CompletedTask
                        : ValueTask.FromException(saveFailure);
                }
            )
            .Verifiable();
        return (added, cancelled);
    }

    // Queues the unit of work that reads the active row after a failed insert.
    private void SetupFindActive(PersistedMasterKey? winner, List<bool>? tokensCancelled = null)
    {
        var (store, _) = EnqueueStoreManager();
        store
            .Setup(x => x.FindActiveAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(
                (SecretOwner _, CancellationToken token) =>
                {
                    tokensCancelled?.Add(token.IsCancellationRequested);
                    return new ValueTask<PersistedMasterKey?>(winner);
                }
            )
            .Verifiable();
    }

    // Queues the unit of work that re-activates a retired row.
    private void SetupRevert(
        PersistedMasterKey retired,
        List<DateTimeOffset?> retiredWhenSaved,
        List<bool>? tokensCancelled = null
    )
    {
        var (store, manager) = EnqueueStoreManager();
        store
            .Setup(x =>
                x.FindByKeyIdsAsync(
                    NormalizedOwner,
                    It.Is<IReadOnlyCollection<Guid>>(ids => ids.Single() == retired.KeyId),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (SecretOwner _, IReadOnlyCollection<Guid> _, CancellationToken token) =>
                {
                    tokensCancelled?.Add(token.IsCancellationRequested);
                    return new ValueTask<IReadOnlyList<PersistedMasterKey>>([retired]);
                }
            )
            .Verifiable();
        store
            .Setup(x => x.UpdateAsync(retired, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                retiredWhenSaved.Add(retired.RetiredWhen);
                return ValueTask.CompletedTask;
            })
            .Verifiable();
    }

    #region GetStatusAsync Tests

    [Fact]
    public async Task GetStatus_MapsEveryRowWithoutMaterial()
    {
        var retired = CreateRow(1, _time.GetUtcNow().AddDays(-400), _time.GetUtcNow().AddDays(-35));
        var active = CreateRow(2, _time.GetUtcNow().AddDays(-35));
        SetupList(active, retired);
        _recovery.SetupGet(x => x.IsConfigured).Returns(true).Verifiable();

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

    [Fact]
    public async Task GetStatus_WithAnOwnerRootKey_CarriesItWithoutMaterial()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-1));
        active.RootKeyReference = "kms://tenant-a/root";
        SetupListWithRootKey(CreateOwnerRootKey(), active);
        _recovery.SetupGet(x => x.IsConfigured).Returns(true).Verifiable();

        var status = await CreateManager().GetStatusAsync(Owner, CancellationToken.None);

        Assert.Equal("kms://tenant-a/root", status.RootKey?.KeyReference);
        Assert.Equal(OwnerRootKeyStates.Active, status.RootKey?.State);
        Assert.Equal("kms://tenant-a/root", Assert.Single(status.Keys).RootKeyReference);
    }

    #endregion

    #region RotateAsync Tests

    private PersistedOwnerRootKey CreateOwnerRootKey(string state = OwnerRootKeyStates.Active) =>
        new()
        {
            OwnerType = NormalizedOwner.OwnerType,
            OwnerId = NormalizedOwner.OwnerId,
            KeyWrapperName = "fake-kms",
            KeyReference = "kms://tenant-a/root",
            State = state,
            CreatedWhen = _time.GetUtcNow(),
            StateChangedWhen = _time.GetUtcNow(),
        };

    [Fact]
    public async Task Rotate_OfAnOwnerWithAnActiveRootKey_WrapsTheSuccessorUnderItWithoutARecoveryCopy()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        var fake = new FakeReferencedKeyWrapper();
        _catalog.Setup(x => x.GetReferenced("fake-kms")).Returns(fake).Verifiable();
        _recovery.SetupGet(x => x.IsConfigured).Returns(true);
        SetupListWithRootKey(CreateOwnerRootKey(), active);
        SetupRetire(active);
        var (added, _) = SetupInsert();
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        var successor = Assert.Single(added);
        Assert.Equal("fake-kms", successor.RootKeyProvider);
        Assert.Equal("kms://tenant-a/root", successor.RootKeyReference);
        Assert.Null(successor.RecoveryWrappedKey);
        Assert.Equal("kms://tenant-a/root", rotation.ActiveKey.RootKeyReference);
        Assert.Equal(1, fake.WrapCount);
    }

    [Fact]
    public async Task Rotate_OfAnOwnerWhoseRootKeyIsUnavailable_ThrowsAndWritesNothing()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        SetupListWithRootKey(CreateOwnerRootKey(OwnerRootKeyStates.Unavailable), active);

        await Assert.ThrowsAsync<OwnerRootKeyUnavailableException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Rotate_OfAnOwnerWhoseRootKeyIsBeingRemoved_WrapsTheSuccessorUnderTheDeploymentRoot()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupListWithRootKey(CreateOwnerRootKey(OwnerRootKeyStates.Removing), active);
        SetupRetire(active);
        var (added, _) = SetupInsert();
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Null(Assert.Single(added).RootKeyReference);
    }

    [Fact]
    public async Task Rotate_RetiresTheActiveRowAndStoresTheNextVersion()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active);
        var (added, _) = SetupInsert();
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        var successor = Assert.Single(added);
        Assert.Equal(2, successor.Version);
        Assert.Null(successor.RetiredWhen);
        Assert.Equal(_time.GetUtcNow(), active.RetiredWhen);
        Assert.Equal(successor.KeyId, rotation.ActiveKey.KeyId);
        Assert.Equal(active.KeyId, rotation.RetiredKey?.KeyId);
        Assert.False(rotation.RetiredKey?.IsActive);
    }

    [Fact]
    public async Task Rotate_WithoutAnyKey_CreatesTheFirstKeyAndReportsNoRetiredKey()
    {
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList();
        var (added, _) = SetupInsert();
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Null(rotation.RetiredKey);
        Assert.Equal(1, Assert.Single(added).Version);
    }

    [Fact]
    public async Task Rotate_WhenTheRootKeyProviderFails_WritesNothingAndNotifiesNobody()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        SetupList(active);
        var wrapper = _mocks.Create<IKeyWrapper>();
        wrapper
            .Setup(x =>
                x.WrapAsync(
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<ReadOnlyMemory<byte>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Throws(new System.Security.Cryptography.CryptographicException("root key down"))
            .Verifiable();
        _catalog.Setup(x => x.GetConfigured()).Returns(wrapper.Object).Verifiable();

        await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );

        Assert.Null(active.RetiredWhen);
        Assert.Empty(_observer.Invocations);
    }

    [Fact]
    public async Task Rotate_WhenTheInsertFailsAndNoKeyIsActive_ReactivatesTheRetiredRowAndThrows()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active);
        SetupInsert(new InvalidOperationException("insert failed"));
        SetupFindActive(null);
        var saved = new List<DateTimeOffset?>();
        SetupRevert(active, saved);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );

        Assert.Equal("insert failed", exception.Message);
        Assert.Null(active.RetiredWhen);
        Assert.Equal([(DateTimeOffset?)null], saved);
        Assert.Empty(_observer.Invocations);
    }

    [Fact]
    public async Task Rotate_WhenTheInsertTimesOut_ReactivatesTheRetiredRowOnAFreshToken()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active);
        SetupInsert(
            new OperationCanceledException("insert timed out"),
            _ => _time.Advance(TimeSpan.FromSeconds(30))
        );
        var findTokensCancelled = new List<bool>();
        SetupFindActive(null, findTokensCancelled);
        var saved = new List<DateTimeOffset?>();
        var revertTokensCancelled = new List<bool>();
        SetupRevert(active, saved, revertTokensCancelled);

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );

        Assert.Equal([false], findTokensCancelled);
        Assert.Equal([false], revertTokensCancelled);
        Assert.Equal([(DateTimeOffset?)null], saved);
        Assert.Null(active.RetiredWhen);
        Assert.Empty(_observer.Invocations);
    }

    [Fact]
    public async Task Rotate_WhenAnotherReplicaStoredTheSuccessorFirst_AdoptsItAndAnnounces()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        var winner = CreateRow(2, _time.GetUtcNow());
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active);
        SetupInsert(new StoreConcurrencyException());
        SetupFindActive(winner);
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Equal(winner.KeyId, rotation.ActiveKey.KeyId);
        Assert.Equal(active.KeyId, rotation.RetiredKey?.KeyId);
    }

    [Fact]
    public async Task Rotate_WhenAnotherWriterCreatedTheFirstKeyFirst_ThrowsStoreConcurrencyException()
    {
        var winner = CreateRow(1, _time.GetUtcNow());
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList();
        SetupInsert(new StoreConcurrencyException());
        SetupFindActive(winner);

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Rotate_WhenTheCallerCancelsAfterTheRetire_StillStoresTheSuccessor()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        using var caller = new CancellationTokenSource();
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active, caller.Cancel);
        var (added, cancelled) = SetupInsert();
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, caller.Token);

        Assert.Single(added);
        Assert.All(cancelled, Assert.False);
        Assert.Equal(added[0].KeyId, rotation.ActiveKey.KeyId);
    }

    [Fact]
    public async Task Rotate_WithRetiredKeysAndNoActiveKey_ThrowsStoreConcurrencyException()
    {
        SetupList(CreateRow(1, _time.GetUtcNow().AddDays(-400), _time.GetUtcNow().AddDays(-100)));

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Rotate_WithinTwiceTheRefreshIntervalOfTheLastRotation_ThrowsTooSoon()
    {
        var retired = CreateRow(
            1,
            _time.GetUtcNow().AddDays(-400),
            _time.GetUtcNow().AddMinutes(-4)
        );
        var active = CreateRow(2, _time.GetUtcNow().AddMinutes(-4));
        SetupList(active, retired);

        var exception = await Assert.ThrowsAsync<MasterKeyRotationTooSoonException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );

        Assert.Equal(TimeSpan.FromMinutes(6), exception.RetryAfter);
        Assert.Equal(Owner, exception.Owner);
        Assert.Null(active.RetiredWhen);
    }

    [Fact]
    public async Task Rotate_AfterTwiceTheRefreshInterval_Rotates()
    {
        var retired = CreateRow(
            1,
            _time.GetUtcNow().AddDays(-400),
            _time.GetUtcNow().AddMinutes(-10)
        );
        var active = CreateRow(2, _time.GetUtcNow().AddMinutes(-10));
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active, retired);
        SetupRetire(active);
        var (added, _) = SetupInsert();
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Equal(3, Assert.Single(added).Version);
    }

    [Fact]
    public async Task Rotate_WhenAnotherRotationCommittedFirst_PropagatesAndNotifiesNobody()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active);
        var (store, manager) = EnqueueStoreManager();
        store
            .Setup(x => x.UpdateAsync(active, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Throws(new StoreConcurrencyException())
            .Verifiable();

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );

        Assert.Empty(_observer.Invocations);
    }

    [Fact]
    public async Task Rotate_WhenAnObserverThrows_StillReturnsTheRotation()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active);
        SetupInsert();
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        _observer
            .Setup(x =>
                x.RotatedAsync(It.IsAny<MasterKeyRotation>(), It.IsAny<CancellationToken>())
            )
            .Throws(new InvalidOperationException("boom"))
            .Verifiable(Times.Once());

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Equal(2, rotation.ActiveKey.Version);
    }

    #endregion

    #region RewrapAsync Tests

    private static bool IsSet(IReadOnlySet<Guid> actual, params Guid[] expected) =>
        actual.SetEquals(expected);

    [Fact]
    public async Task Rewrap_AsksEachSourceOnceWithEveryRetiredKey()
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
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupList(active, retiredNew, retiredOld);
        _source
            .Setup(x =>
                x.RewrapAsync(
                    Owner,
                    It.Is<IReadOnlySet<Guid>>(ids =>
                        IsSet(ids, retiredNew.KeyId, retiredOld.KeyId)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<ProtectedValueRewrapResult>(new ProtectedValueRewrapResult(3, 1))
            )
            .Verifiable(Times.Once());
        _otherSource
            .Setup(x =>
                x.RewrapAsync(
                    Owner,
                    It.Is<IReadOnlySet<Guid>>(ids =>
                        IsSet(ids, retiredNew.KeyId, retiredOld.KeyId)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                new ValueTask<ProtectedValueRewrapResult>(new ProtectedValueRewrapResult(4, 0))
            )
            .Verifiable(Times.Once());

        var result = await CreateManager().RewrapAsync(Owner, CancellationToken.None);

        Assert.Equal(new ProtectedValueRewrapResult(7, 1), result);
    }

    [Fact]
    public async Task Rewrap_WithoutARetiredKey_AsksNoSource()
    {
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupList(CreateRow(1, _time.GetUtcNow().AddDays(-1)));

        var result = await CreateManager().RewrapAsync(Owner, CancellationToken.None);

        Assert.Equal(ProtectedValueRewrapResult.None, result);
        Assert.Empty(_source.Invocations);
        Assert.Empty(_otherSource.Invocations);
    }

    #endregion

    #region DestroyRetiredAsync Tests

    private void SetupDestroy(
        PersistedMasterKey row,
        bool removed = true,
        Exception? saveFailure = null
    )
    {
        var (store, manager) = EnqueueStoreManager();
        store
            .Setup(x => x.RemoveAsync(row, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(removed))
            .Verifiable();
        if (removed)
        {
            manager
                .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(() =>
                    saveFailure is null
                        ? ValueTask.CompletedTask
                        : ValueTask.FromException(saveFailure)
                )
                .Verifiable();
        }
    }

    private void SetupDestroyed(Guid keyId) =>
        _observer
            .Setup(x =>
                x.DestroyedAsync(
                    It.Is<MasterKeyInfo>(info => info.KeyId == keyId),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());

    private static void SetupReferenced(
        Mock<IProtectedValueSource> source,
        Guid[] candidates,
        params Guid[] referenced
    ) =>
        source
            .Setup(x =>
                x.FindReferencedAsync(
                    Owner,
                    It.Is<IReadOnlySet<Guid>>(ids => IsSet(ids, candidates)),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<IReadOnlySet<Guid>>(new HashSet<Guid>(referenced)))
            .Verifiable(Times.Once());

    [Fact]
    public async Task Destroy_RemovesAPastGraceUnreferencedKeyAndNotifies()
    {
        var retired = CreateRow(1, _time.GetUtcNow().AddDays(-400), _time.GetUtcNow().AddDays(-31));
        SetupList(retired);
        SetupReferenced(_source, [retired.KeyId]);
        SetupReferenced(_otherSource, [retired.KeyId]);
        SetupDestroy(retired);
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupDestroyed(retired.KeyId);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Equal(retired.KeyId, Assert.Single(destroyed).KeyId);
    }

    [Fact]
    public async Task Destroy_AsksEachSourceOnceForEveryCandidate()
    {
        var first = CreateRow(1, _time.GetUtcNow().AddDays(-800), _time.GetUtcNow().AddDays(-60));
        var second = CreateRow(2, _time.GetUtcNow().AddDays(-60), _time.GetUtcNow().AddDays(-31));
        var inside = CreateRow(3, _time.GetUtcNow().AddDays(-31), _time.GetUtcNow().AddDays(-1));
        SetupList(inside, second, first);
        SetupReferenced(_source, [first.KeyId, second.KeyId]);
        SetupReferenced(_otherSource, [first.KeyId, second.KeyId]);
        SetupDestroy(second);
        SetupDestroy(first);
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupDestroyed(first.KeyId);
        SetupDestroyed(second.KeyId);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Equal(2, destroyed.Count);
    }

    [Fact]
    public async Task Destroy_KeepsTheKeyASourceReportsAndDestroysTheOther()
    {
        var kept = CreateRow(1, _time.GetUtcNow().AddDays(-800), _time.GetUtcNow().AddDays(-60));
        var gone = CreateRow(2, _time.GetUtcNow().AddDays(-60), _time.GetUtcNow().AddDays(-31));
        SetupList(gone, kept);
        SetupReferenced(_source, [kept.KeyId, gone.KeyId], kept.KeyId);
        SetupReferenced(_otherSource, [kept.KeyId, gone.KeyId]);
        SetupDestroy(gone);
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupDestroyed(gone.KeyId);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Equal(gone.KeyId, Assert.Single(destroyed).KeyId);
    }

    [Fact]
    public async Task Destroy_WhenALaterRemovalFails_AnnouncesTheKeysAlreadyDestroyedAndThrows()
    {
        var first = CreateRow(1, _time.GetUtcNow().AddDays(-800), _time.GetUtcNow().AddDays(-60));
        var second = CreateRow(2, _time.GetUtcNow().AddDays(-60), _time.GetUtcNow().AddDays(-31));
        SetupList(first, second);
        SetupReferenced(_source, [first.KeyId, second.KeyId]);
        SetupReferenced(_otherSource, [first.KeyId, second.KeyId]);
        SetupDestroy(first);
        SetupDestroy(second, saveFailure: new StoreConcurrencyException());
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupDestroyed(first.KeyId);

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Destroy_WhenCancelledAfterARemoval_AnnouncesTheDestroyedKey()
    {
        var first = CreateRow(1, _time.GetUtcNow().AddDays(-800), _time.GetUtcNow().AddDays(-60));
        var second = CreateRow(2, _time.GetUtcNow().AddDays(-60), _time.GetUtcNow().AddDays(-31));
        SetupList(first, second);
        SetupReferenced(_source, [first.KeyId, second.KeyId]);
        SetupReferenced(_otherSource, [first.KeyId, second.KeyId]);
        SetupDestroy(first);
        SetupDestroy(second, saveFailure: new OperationCanceledException());
        _provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupDestroyed(first.KeyId);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Destroy_KeepsAKeyInsideTheGrace()
    {
        var retired = CreateRow(1, _time.GetUtcNow().AddDays(-400), _time.GetUtcNow().AddDays(-29));
        SetupList(retired);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Empty(destroyed);
        Assert.Empty(_source.Invocations);
    }

    [Fact]
    public async Task Destroy_KeepsAKeyThatASourceReferences()
    {
        var retired = CreateRow(1, _time.GetUtcNow().AddDays(-400), _time.GetUtcNow().AddDays(-31));
        SetupList(retired);
        SetupReferenced(_source, [retired.KeyId], retired.KeyId);
        SetupReferenced(_otherSource, [retired.KeyId]);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Empty(destroyed);
        Assert.Empty(_observer.Invocations);
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
