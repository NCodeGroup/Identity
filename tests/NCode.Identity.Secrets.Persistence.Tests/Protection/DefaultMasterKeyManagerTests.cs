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

public class DefaultMasterKeyManagerTests : BaseTests
{
    private static SecretOwner Owner { get; } = SecretOwner.ForTenant("Tenant-A");
    private static SecretOwner NormalizedOwner { get; } = Owner.Normalize();

    private Mock<ISecretProtectorProvider> Provider { get; }
    private Mock<IStoreManagerFactory> StoreManagerFactory { get; }
    private Mock<IRecoveryKeyWrapper> Recovery { get; }
    private Mock<IKeyWrapperCatalog> Catalog { get; }
    private EphemeralKeyWrapper RootKey { get; } = new();
    private Mock<IProtectedValueSource> Source { get; }
    private Mock<IProtectedValueSource> OtherSource { get; }
    private Mock<IMasterKeyChangeObserver> Observer { get; }
    private Queue<IStoreManager> StoreManagers { get; } = new();
    private FakeTimeProvider Time { get; } =
        new(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
    private MasterKeyOptions KeyOptions { get; } = new();
    private bool StoreManagerFactorySetUp { get; set; }

    public DefaultMasterKeyManagerTests()
    {
        Provider = CreateStrictMock<ISecretProtectorProvider>();
        StoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        Recovery = CreateStrictMock<IRecoveryKeyWrapper>();
        Catalog = CreateStrictMock<IKeyWrapperCatalog>();
        Source = CreateStrictMock<IProtectedValueSource>();
        OtherSource = CreateStrictMock<IProtectedValueSource>();
        Observer = CreateStrictMock<IMasterKeyChangeObserver>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            RootKey.Dispose();
        }

        base.Dispose(disposing);

        if (disposing)
        {
            Assert.Empty(StoreManagers);
        }
    }

    private DefaultMasterKeyManager CreateManager() =>
        new(
            Provider.Object,
            StoreManagerFactory.Object,
            Recovery.Object,
            new MasterKeyFactory(Catalog.Object, Recovery.Object, Time),
            [Source.Object, OtherSource.Object],
            [Observer.Object],
            Microsoft.Extensions.Options.Options.Create(
                new SecretProtectionOptions { MasterKeys = KeyOptions }
            ),
            Time,
            NullLogger<DefaultMasterKeyManager>.Instance
        );

    private void SetupRootKey() =>
        Catalog.Setup(x => x.GetConfigured()).Returns(RootKey).Verifiable();

    private (Mock<IMasterKeyStore> Store, Mock<IStoreManager> Manager) EnqueueStoreManager()
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
        StoreManagers.Enqueue(manager.Object);
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
    // none here; the other operations do not read it, so its setups may go unused.
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

        var rootKeyStore = CreateStrictMock<IOwnerRootKeyStore>();
        rootKeyStore
            .Setup(x => x.FindAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PersistedOwnerRootKey?>(rootKey))
            .Verifiable(Times.AtMostOnce());
        manager
            .Setup(x => x.GetStore<IOwnerRootKeyStore>())
            .Returns(rootKeyStore.Object)
            .Verifiable(Times.AtMostOnce());
    }

    private void SetupObserverRotated()
    {
        Observer
            .Setup(x =>
                x.RotatedAsync(It.IsAny<MasterKeyRotation>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
    }

    private void SetupRecoveryNotConfigured() =>
        Recovery.SetupGet(x => x.IsConfigured).Returns(false).Verifiable();

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
        var retired = CreateRow(1, Time.GetUtcNow().AddDays(-400), Time.GetUtcNow().AddDays(-35));
        var active = CreateRow(2, Time.GetUtcNow().AddDays(-35));
        SetupList(active, retired);
        Recovery.SetupGet(x => x.IsConfigured).Returns(true).Verifiable();

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
        var active = CreateRow(1, Time.GetUtcNow().AddDays(-1));
        active.RootKeyReference = "kms://tenant-a/root";
        SetupListWithRootKey(CreateOwnerRootKey(), active);
        Recovery.SetupGet(x => x.IsConfigured).Returns(true).Verifiable();

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
            CreatedWhen = Time.GetUtcNow(),
            StateChangedWhen = Time.GetUtcNow(),
        };

    [Fact]
    public async Task Rotate_OfAnOwnerWithAnActiveRootKey_WrapsTheSuccessorUnderItWithoutARecoveryCopy()
    {
        var active = CreateRow(1, Time.GetUtcNow().AddDays(-400));
        var fake = new FakeReferencedKeyWrapper();
        Catalog.Setup(x => x.GetReferenced("fake-kms")).Returns(fake).Verifiable();
        Recovery.SetupGet(x => x.IsConfigured).Returns(true);
        SetupListWithRootKey(CreateOwnerRootKey(), active);
        SetupRetire(active);
        var (added, _) = SetupInsert();
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
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
        var active = CreateRow(1, Time.GetUtcNow().AddDays(-400));
        SetupListWithRootKey(CreateOwnerRootKey(OwnerRootKeyStates.Unavailable), active);

        await Assert.ThrowsAsync<OwnerRootKeyUnavailableException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Rotate_OfAnOwnerWhoseRootKeyIsBeingRemoved_WrapsTheSuccessorUnderTheDeploymentRoot()
    {
        var active = CreateRow(1, Time.GetUtcNow().AddDays(-400));
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupListWithRootKey(CreateOwnerRootKey(OwnerRootKeyStates.Removing), active);
        SetupRetire(active);
        var (added, _) = SetupInsert();
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Null(Assert.Single(added).RootKeyReference);
    }

    [Fact]
    public async Task Rotate_RetiresTheActiveRowAndStoresTheNextVersion()
    {
        var active = CreateRow(1, Time.GetUtcNow().AddDays(-400));
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active);
        var (added, _) = SetupInsert();
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        var successor = Assert.Single(added);
        Assert.Equal(2, successor.Version);
        Assert.Null(successor.RetiredWhen);
        Assert.Equal(Time.GetUtcNow(), active.RetiredWhen);
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
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Null(rotation.RetiredKey);
        Assert.Equal(1, Assert.Single(added).Version);
    }

    [Fact]
    public async Task Rotate_WhenTheRootKeyProviderFails_WritesNothingAndNotifiesNobody()
    {
        var active = CreateRow(1, Time.GetUtcNow().AddDays(-400));
        SetupList(active);
        var wrapper = CreateStrictMock<IKeyWrapper>();
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
        Catalog.Setup(x => x.GetConfigured()).Returns(wrapper.Object).Verifiable();

        await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );

        Assert.Null(active.RetiredWhen);
        Assert.Empty(Observer.Invocations);
    }

    [Fact]
    public async Task Rotate_WhenTheInsertFailsAndNoKeyIsActive_ReactivatesTheRetiredRowAndThrows()
    {
        var active = CreateRow(1, Time.GetUtcNow().AddDays(-400));
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
        Assert.Empty(Observer.Invocations);
    }

    [Fact]
    public async Task Rotate_WhenTheInsertTimesOut_ReactivatesTheRetiredRowOnAFreshToken()
    {
        var active = CreateRow(1, Time.GetUtcNow().AddDays(-400));
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active);
        SetupInsert(
            new OperationCanceledException("insert timed out"),
            _ => Time.Advance(TimeSpan.FromSeconds(30))
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
        Assert.Empty(Observer.Invocations);
    }

    [Fact]
    public async Task Rotate_WhenAnotherReplicaStoredTheSuccessorFirst_AdoptsItAndAnnounces()
    {
        var active = CreateRow(1, Time.GetUtcNow().AddDays(-400));
        var winner = CreateRow(2, Time.GetUtcNow());
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active);
        SetupInsert(new StoreConcurrencyException());
        SetupFindActive(winner);
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Equal(winner.KeyId, rotation.ActiveKey.KeyId);
        Assert.Equal(active.KeyId, rotation.RetiredKey?.KeyId);
    }

    [Fact]
    public async Task Rotate_WhenAnotherWriterCreatedTheFirstKeyFirst_ThrowsStoreConcurrencyException()
    {
        var winner = CreateRow(1, Time.GetUtcNow());
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
        var active = CreateRow(1, Time.GetUtcNow().AddDays(-400));
        using var caller = new CancellationTokenSource();
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active, caller.Cancel);
        var (added, cancelled) = SetupInsert();
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, caller.Token);

        Assert.Single(added);
        Assert.All(cancelled, Assert.False);
        Assert.Equal(added[0].KeyId, rotation.ActiveKey.KeyId);
    }

    [Fact]
    public async Task Rotate_WithRetiredKeysAndNoActiveKey_ThrowsStoreConcurrencyException()
    {
        SetupList(CreateRow(1, Time.GetUtcNow().AddDays(-400), Time.GetUtcNow().AddDays(-100)));

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Rotate_WithinTwiceTheRefreshIntervalOfTheLastRotation_ThrowsTooSoon()
    {
        var retired = CreateRow(1, Time.GetUtcNow().AddDays(-400), Time.GetUtcNow().AddMinutes(-4));
        var active = CreateRow(2, Time.GetUtcNow().AddMinutes(-4));
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
            Time.GetUtcNow().AddDays(-400),
            Time.GetUtcNow().AddMinutes(-10)
        );
        var active = CreateRow(2, Time.GetUtcNow().AddMinutes(-10));
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active, retired);
        SetupRetire(active);
        var (added, _) = SetupInsert();
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupObserverRotated();

        await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Equal(3, Assert.Single(added).Version);
    }

    [Fact]
    public async Task Rotate_WhenAnotherRotationCommittedFirst_PropagatesAndNotifiesNobody()
    {
        var active = CreateRow(1, Time.GetUtcNow().AddDays(-400));
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

        Assert.Empty(Observer.Invocations);
    }

    [Fact]
    public async Task Rotate_WhenAnObserverThrows_StillReturnsTheRotation()
    {
        var active = CreateRow(1, Time.GetUtcNow().AddDays(-400));
        SetupRootKey();
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active);
        SetupInsert();
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        Observer
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
            Time.GetUtcNow().AddDays(-800),
            Time.GetUtcNow().AddDays(-400)
        );
        var retiredNew = CreateRow(2, Time.GetUtcNow().AddDays(-400), Time.GetUtcNow().AddDays(-1));
        var active = CreateRow(3, Time.GetUtcNow().AddDays(-1));
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupList(active, retiredNew, retiredOld);
        Source
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
        OtherSource
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
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupList(CreateRow(1, Time.GetUtcNow().AddDays(-1)));

        var result = await CreateManager().RewrapAsync(Owner, CancellationToken.None);

        Assert.Equal(ProtectedValueRewrapResult.None, result);
        Assert.Empty(Source.Invocations);
        Assert.Empty(OtherSource.Invocations);
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
        Observer
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
        var retired = CreateRow(1, Time.GetUtcNow().AddDays(-400), Time.GetUtcNow().AddDays(-31));
        SetupList(retired);
        SetupReferenced(Source, [retired.KeyId]);
        SetupReferenced(OtherSource, [retired.KeyId]);
        SetupDestroy(retired);
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupDestroyed(retired.KeyId);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Equal(retired.KeyId, Assert.Single(destroyed).KeyId);
    }

    [Fact]
    public async Task Destroy_AsksEachSourceOnceForEveryCandidate()
    {
        var first = CreateRow(1, Time.GetUtcNow().AddDays(-800), Time.GetUtcNow().AddDays(-60));
        var second = CreateRow(2, Time.GetUtcNow().AddDays(-60), Time.GetUtcNow().AddDays(-31));
        var inside = CreateRow(3, Time.GetUtcNow().AddDays(-31), Time.GetUtcNow().AddDays(-1));
        SetupList(inside, second, first);
        SetupReferenced(Source, [first.KeyId, second.KeyId]);
        SetupReferenced(OtherSource, [first.KeyId, second.KeyId]);
        SetupDestroy(second);
        SetupDestroy(first);
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupDestroyed(first.KeyId);
        SetupDestroyed(second.KeyId);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Equal(2, destroyed.Count);
    }

    [Fact]
    public async Task Destroy_KeepsTheKeyASourceReportsAndDestroysTheOther()
    {
        var kept = CreateRow(1, Time.GetUtcNow().AddDays(-800), Time.GetUtcNow().AddDays(-60));
        var gone = CreateRow(2, Time.GetUtcNow().AddDays(-60), Time.GetUtcNow().AddDays(-31));
        SetupList(gone, kept);
        SetupReferenced(Source, [kept.KeyId, gone.KeyId], kept.KeyId);
        SetupReferenced(OtherSource, [kept.KeyId, gone.KeyId]);
        SetupDestroy(gone);
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupDestroyed(gone.KeyId);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Equal(gone.KeyId, Assert.Single(destroyed).KeyId);
    }

    [Fact]
    public async Task Destroy_WhenALaterRemovalFails_AnnouncesTheKeysAlreadyDestroyedAndThrows()
    {
        var first = CreateRow(1, Time.GetUtcNow().AddDays(-800), Time.GetUtcNow().AddDays(-60));
        var second = CreateRow(2, Time.GetUtcNow().AddDays(-60), Time.GetUtcNow().AddDays(-31));
        SetupList(first, second);
        SetupReferenced(Source, [first.KeyId, second.KeyId]);
        SetupReferenced(OtherSource, [first.KeyId, second.KeyId]);
        SetupDestroy(first);
        SetupDestroy(second, saveFailure: new StoreConcurrencyException());
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupDestroyed(first.KeyId);

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Destroy_WhenCancelledAfterARemoval_AnnouncesTheDestroyedKey()
    {
        var first = CreateRow(1, Time.GetUtcNow().AddDays(-800), Time.GetUtcNow().AddDays(-60));
        var second = CreateRow(2, Time.GetUtcNow().AddDays(-60), Time.GetUtcNow().AddDays(-31));
        SetupList(first, second);
        SetupReferenced(Source, [first.KeyId, second.KeyId]);
        SetupReferenced(OtherSource, [first.KeyId, second.KeyId]);
        SetupDestroy(first);
        SetupDestroy(second, saveFailure: new OperationCanceledException());
        Provider.Setup(x => x.Evict(Owner)).Verifiable(Times.Once());
        SetupDestroyed(first.KeyId);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Destroy_KeepsAKeyInsideTheGrace()
    {
        var retired = CreateRow(1, Time.GetUtcNow().AddDays(-400), Time.GetUtcNow().AddDays(-29));
        SetupList(retired);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Empty(destroyed);
        Assert.Empty(Source.Invocations);
    }

    [Fact]
    public async Task Destroy_KeepsAKeyThatASourceReferences()
    {
        var retired = CreateRow(1, Time.GetUtcNow().AddDays(-400), Time.GetUtcNow().AddDays(-31));
        SetupList(retired);
        SetupReferenced(Source, [retired.KeyId], retired.KeyId);
        SetupReferenced(OtherSource, [retired.KeyId]);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Empty(destroyed);
        Assert.Empty(Observer.Invocations);
    }

    [Fact]
    public async Task Destroy_KeepsTheActiveKey()
    {
        SetupList(CreateRow(1, Time.GetUtcNow().AddDays(-400)));

        Assert.Empty(await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None));
    }

    [Fact]
    public async Task Destroy_WhenDestructionIsOff_ReadsNothing()
    {
        KeyOptions.DestroyRetired = false;

        Assert.Empty(await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None));
    }

    #endregion
}
