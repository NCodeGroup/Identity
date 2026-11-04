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

    public DefaultMasterKeyManagerTests()
    {
        _provider = _mocks.Create<ISecretProtectorProvider>();
        _storeManagerFactory = _mocks.Create<IStoreManagerFactory>();
        _recovery = _mocks.Create<IRecoveryKeyWrapper>();
        _catalog = _mocks.Create<IKeyWrapperCatalog>();
        _catalog.Setup(x => x.GetConfigured()).Returns(_rootKey);
        _source = _mocks.Create<IProtectedValueSource>();
        _otherSource = _mocks.Create<IProtectedValueSource>();
        _observer = _mocks.Create<IMasterKeyChangeObserver>();
        _storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<IStoreManager>(_storeManagers.Dequeue()));
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

    private void SetupRecoveryNotConfigured() =>
        _recovery.SetupGet(x => x.IsConfigured).Returns(false);

    // Queues the unit of work that retires the active row.
    private void SetupRetire(PersistedMasterKey active, Action? onSave = null)
    {
        var (store, manager) = EnqueueStoreManager();
        store
            .Setup(x => x.UpdateAsync(active, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                onSave?.Invoke();
                return ValueTask.CompletedTask;
            });
    }

    // Queues the unit of work that stores the successor; the saved row and the tokens of the add and the save are
    // captured.
    private (List<PersistedMasterKey> Added, List<bool> TokensCancelled) SetupInsert(
        Exception? saveFailure = null
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
            );
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(
                (CancellationToken token) =>
                {
                    cancelled.Add(token.IsCancellationRequested);
                    return saveFailure is null
                        ? ValueTask.CompletedTask
                        : ValueTask.FromException(saveFailure);
                }
            );
        return (added, cancelled);
    }

    // Queues the unit of work that reads the active row after a failed insert.
    private void SetupFindActive(PersistedMasterKey? winner)
    {
        var (store, _) = EnqueueStoreManager();
        store
            .Setup(x => x.FindActiveAsync(NormalizedOwner, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<PersistedMasterKey?>(winner));
    }

    // Queues the unit of work that re-activates a retired row.
    private void SetupRevert(PersistedMasterKey retired, List<DateTimeOffset?> retiredWhenSaved)
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
            .Returns(new ValueTask<IReadOnlyList<PersistedMasterKey>>([retired]));
        store
            .Setup(x => x.UpdateAsync(retired, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                retiredWhenSaved.Add(retired.RetiredWhen);
                return ValueTask.CompletedTask;
            });
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
    public async Task Rotate_RetiresTheActiveRowAndStoresTheNextVersion()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active);
        var (added, _) = SetupInsert();
        _provider.Setup(x => x.Evict(Owner));
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        var successor = Assert.Single(added);
        Assert.Equal(2, successor.Version);
        Assert.Null(successor.RetiredWhen);
        Assert.Equal(_time.GetUtcNow(), active.RetiredWhen);
        Assert.Equal(successor.KeyId, rotation.ActiveKey.KeyId);
        Assert.Equal(active.KeyId, rotation.RetiredKey?.KeyId);
        Assert.False(rotation.RetiredKey?.IsActive);
        _observer.Verify(x => x.RotatedAsync(rotation, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Rotate_WithoutAnyKey_CreatesTheFirstKeyAndReportsNoRetiredKey()
    {
        SetupRecoveryNotConfigured();
        SetupList();
        var (added, _) = SetupInsert();
        _provider.Setup(x => x.Evict(Owner));
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Null(rotation.RetiredKey);
        Assert.Equal(1, Assert.Single(added).Version);
    }

    [Fact]
    public async Task Rotate_WhenTheRootKeyProviderFails_WritesNothingAndNotifiesNobody()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        SetupRecoveryNotConfigured();
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
            .Throws(new System.Security.Cryptography.CryptographicException("root key down"));
        _catalog.Setup(x => x.GetConfigured()).Returns(wrapper.Object);

        await Assert.ThrowsAsync<System.Security.Cryptography.CryptographicException>(async () =>
            await CreateManager().RotateAsync(Owner, CancellationToken.None)
        );

        Assert.Null(active.RetiredWhen);
    }

    [Fact]
    public async Task Rotate_WhenTheInsertFailsAndNoKeyIsActive_ReactivatesTheRetiredRowAndThrows()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
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
    }

    [Fact]
    public async Task Rotate_WhenAnotherReplicaStoredTheSuccessorFirst_AdoptsItAndAnnounces()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        var winner = CreateRow(2, _time.GetUtcNow());
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active);
        SetupInsert(new StoreConcurrencyException());
        SetupFindActive(winner);
        _provider.Setup(x => x.Evict(Owner));
        SetupObserverRotated();

        var rotation = await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Equal(winner.KeyId, rotation.ActiveKey.KeyId);
        Assert.Equal(active.KeyId, rotation.RetiredKey?.KeyId);
    }

    [Fact]
    public async Task Rotate_WhenAnotherWriterCreatedTheFirstKeyFirst_ThrowsStoreConcurrencyException()
    {
        var winner = CreateRow(1, _time.GetUtcNow());
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
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active, caller.Cancel);
        var (added, cancelled) = SetupInsert();
        _provider.Setup(x => x.Evict(Owner));
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
        SetupRecoveryNotConfigured();
        SetupList(active, retired);
        SetupRetire(active);
        var (added, _) = SetupInsert();
        _provider.Setup(x => x.Evict(Owner));
        SetupObserverRotated();

        await CreateManager().RotateAsync(Owner, CancellationToken.None);

        Assert.Equal(3, Assert.Single(added).Version);
    }

    [Fact]
    public async Task Rotate_WhenAnotherRotationCommittedFirst_PropagatesAndNotifiesNobody()
    {
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        SetupRecoveryNotConfigured();
        SetupList(active);
        var (store, manager) = EnqueueStoreManager();
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
        var active = CreateRow(1, _time.GetUtcNow().AddDays(-400));
        SetupRecoveryNotConfigured();
        SetupList(active);
        SetupRetire(active);
        SetupInsert();
        _provider.Setup(x => x.Evict(Owner));
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
        _provider.Setup(x => x.Evict(Owner));
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
            );
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
            );

        var result = await CreateManager().RewrapAsync(Owner, CancellationToken.None);

        Assert.Equal(new ProtectedValueRewrapResult(7, 1), result);
        _source.Verify(
            x =>
                x.RewrapAsync(
                    It.IsAny<SecretOwner>(),
                    It.IsAny<IReadOnlySet<Guid>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task Rewrap_WithoutARetiredKey_AsksNoSource()
    {
        _provider.Setup(x => x.Evict(Owner));
        SetupList(CreateRow(1, _time.GetUtcNow().AddDays(-1)));

        var result = await CreateManager().RewrapAsync(Owner, CancellationToken.None);

        Assert.Equal(ProtectedValueRewrapResult.None, result);
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
            .Returns(new ValueTask<IReadOnlySet<Guid>>(new HashSet<Guid>(referenced)));

    [Fact]
    public async Task Destroy_RemovesAPastGraceUnreferencedKeyAndNotifies()
    {
        var retired = CreateRow(1, _time.GetUtcNow().AddDays(-400), _time.GetUtcNow().AddDays(-31));
        SetupList(retired);
        SetupReferenced(_source, [retired.KeyId]);
        SetupReferenced(_otherSource, [retired.KeyId]);
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
        _provider.Setup(x => x.Evict(Owner));
        _observer
            .Setup(x => x.DestroyedAsync(It.IsAny<MasterKeyInfo>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Equal(2, destroyed.Count);
        _source.Verify(
            x =>
                x.FindReferencedAsync(
                    It.IsAny<SecretOwner>(),
                    It.IsAny<IReadOnlySet<Guid>>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
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
        _provider.Setup(x => x.Evict(Owner));
        _observer
            .Setup(x => x.DestroyedAsync(It.IsAny<MasterKeyInfo>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var destroyed = await CreateManager().DestroyRetiredAsync(Owner, CancellationToken.None);

        Assert.Equal(gone.KeyId, Assert.Single(destroyed).KeyId);
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
        SetupReferenced(_source, [retired.KeyId], retired.KeyId);
        SetupReferenced(_otherSource, [retired.KeyId]);

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
