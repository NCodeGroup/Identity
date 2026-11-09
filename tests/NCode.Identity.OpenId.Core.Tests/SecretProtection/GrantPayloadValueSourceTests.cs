// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.OpenId.Core.Tests;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class GrantPayloadValueSourceTests : BaseTests
{
    private static SecretOwner Tenant { get; } = SecretOwner.ForTenant("tenant-1");
    private static Guid OldKey { get; } = Guid.NewGuid();
    private static Guid OlderKey { get; } = Guid.NewGuid();
    private static Guid ActiveKey { get; } = Guid.NewGuid();
    private static IReadOnlySet<Guid> OldKeys { get; } = new HashSet<Guid> { OldKey };

    private readonly Mock<IStoreManagerFactory> _storeManagerFactory;
    private readonly Queue<IStoreManager> _storeManagers = new();
    private readonly FakeRewrapProvider _provider = new(ActiveKey);
    private bool _storeManagerFactorySetUp;

    public GrantPayloadValueSourceTests()
    {
        _storeManagerFactory = CreateStrictMock<IStoreManagerFactory>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            Assert.Empty(_storeManagers);

        base.Dispose(disposing);
    }

    private GrantPayloadValueSource CreateSource() =>
        new(_storeManagerFactory.Object, _provider, NullLogger<GrantPayloadValueSource>.Instance);

    private (Mock<IGrantStore> Store, Mock<IStoreManager> Manager) EnqueueManager()
    {
        if (!_storeManagerFactorySetUp)
        {
            _storeManagerFactorySetUp = true;
            _storeManagerFactory
                .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
                .Returns(() => new ValueTask<IStoreManager>(_storeManagers.Dequeue()))
                .Verifiable();
        }

        var store = CreateStrictMock<IGrantStore>();
        var manager = CreateStrictMock<IStoreManager>();
        manager.Setup(x => x.GetStore<IGrantStore>()).Returns(store.Object).Verifiable();
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        _storeManagers.Enqueue(manager.Object);
        return (store, manager);
    }

    private static PersistedProtectedValue Value(string id, Guid keyId) =>
        new()
        {
            RecordId = id,
            ItemId = id,
            ProtectedValue = FakeRewrapProvider.Value(keyId, id),
            ConcurrencyToken = "token",
        };

    private static PagedResult<PersistedProtectedValue> Page(
        string? next,
        params PersistedProtectedValue[] items
    ) => new() { Items = items, NextCursor = next };

    private static void SetupPage(
        Mock<IGrantStore> store,
        string? cursor,
        string? next,
        params PersistedProtectedValue[] items
    ) => SetupPageFor(store, OldKeys, cursor, next, items);

    // The store is asked for exactly the prefixes of the keys.
    private static void SetupPageFor(
        Mock<IGrantStore> store,
        IReadOnlySet<Guid> keys,
        string? cursor,
        string? next,
        params PersistedProtectedValue[] items
    )
    {
        var expected = keys.Select(key => $"k:{key:D}").ToHashSet();
        store
            .Setup(x =>
                x.GetProtectedPayloadPageAsync(
                    Tenant.OwnerId,
                    It.Is<IReadOnlyCollection<string>>(prefixes => expected.SetEquals(prefixes)),
                    It.Is<PageRequest>(page => page.Cursor == cursor && page.Limit == 100),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PagedResult<PersistedProtectedValue>>(Page(next, items)))
            .Verifiable();
    }

    private static void SetupUpdate(
        Mock<IGrantStore> store,
        PersistedProtectedValue value,
        bool exists = true
    ) =>
        store
            .Setup(x => x.UpdateProtectedPayloadAsync(value, It.IsAny<CancellationToken>()))
            .Returns(new ValueTask<bool>(exists))
            .Verifiable();

    private static void SetupSave(Mock<IStoreManager> manager) =>
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    #region RewrapAsync Tests

    [Fact]
    public async Task Rewrap_RewrapsOnlyTheValuesThatNameTheKeyWithOneProtectorPerPage()
    {
        var stale1 = Value("s1", OldKey);
        var current = Value("s2", ActiveKey);
        var stale2 = Value("s3", OldKey);
        var (store, manager) = EnqueueManager();
        SetupPage(store, null, null, stale1, current, stale2);
        SetupUpdate(store, stale1);
        SetupUpdate(store, stale2);
        SetupSave(manager);

        var result = await CreateSource().RewrapAsync(Tenant, OldKeys, CancellationToken.None);

        Assert.Equal(new ProtectedValueRewrapResult(2, 0), result);
        var request = Assert.Single(_provider.Requests);
        Assert.Equal(2, request.Count);
        Assert.StartsWith($"k:{ActiveKey:D}|", stale1.ProtectedValue, StringComparison.Ordinal);
        Assert.Equal(
            [
                new SecretProtectionContext(SecretProtectionPurposes.GrantPayload, "s1"),
                new SecretProtectionContext(SecretProtectionPurposes.GrantPayload, "s3"),
            ],
            _provider.Contexts
        );
        Assert.StartsWith($"k:{ActiveKey:D}|", stale2.ProtectedValue, StringComparison.Ordinal);
        Assert.Equal(FakeRewrapProvider.Value(ActiveKey, "s2"), current.ProtectedValue);
    }

    [Fact]
    public async Task Rewrap_RewrapsTheValuesUnderAnyRequestedKeyInOnePass()
    {
        var keys = new HashSet<Guid> { OldKey, OlderKey };
        var first = Value("s1", OldKey);
        var second = Value("s2", OlderKey);
        var current = Value("s3", ActiveKey);
        var (store, manager) = EnqueueManager();
        SetupPageFor(store, keys, null, null, first, second, current);
        SetupUpdate(store, first);
        SetupUpdate(store, second);
        SetupSave(manager);

        var result = await CreateSource().RewrapAsync(Tenant, keys, CancellationToken.None);

        Assert.Equal(new ProtectedValueRewrapResult(2, 0), result);
        Assert.Single(_provider.Requests);
    }

    [Fact]
    public async Task Rewrap_ReadsEveryPage()
    {
        var first = Value("s1", OldKey);
        var second = Value("s2", OldKey);
        var (store1, manager1) = EnqueueManager();
        SetupPage(store1, null, "next", first);
        SetupUpdate(store1, first);
        SetupSave(manager1);
        var (store2, manager2) = EnqueueManager();
        SetupPage(store2, "next", null, second);
        SetupUpdate(store2, second);
        SetupSave(manager2);

        var result = await CreateSource().RewrapAsync(Tenant, OldKeys, CancellationToken.None);

        Assert.Equal(new ProtectedValueRewrapResult(2, 0), result);
        Assert.Equal(2, _provider.Requests.Count);
    }

    [Fact]
    public async Task Rewrap_AValueThatCannotBeRewrapped_IsCountedFailedAndTheOthersAreWritten()
    {
        _provider.Unrewrappable.Add("bad");
        var bad = Value("bad", OldKey);
        var good = Value("good", OldKey);
        var (store1, manager1) = EnqueueManager();
        SetupPage(store1, null, "next", bad, good);
        SetupUpdate(store1, good);
        SetupSave(manager1);
        var (store2, _) = EnqueueManager();
        SetupPage(store2, "next", null);

        var result = await CreateSource().RewrapAsync(Tenant, OldKeys, CancellationToken.None);

        Assert.Equal(new ProtectedValueRewrapResult(1, 1), result);
        Assert.Equal(FakeRewrapProvider.Value(OldKey, "bad"), bad.ProtectedValue);
    }

    [Fact]
    public async Task Rewrap_ARowThatVanished_IsSkippedAndThePageIsSaved()
    {
        var gone = Value("gone", OldKey);
        var kept = Value("kept", OldKey);
        var (store, manager) = EnqueueManager();
        SetupPage(store, null, null, gone, kept);
        SetupUpdate(store, gone, exists: false);
        SetupUpdate(store, kept);
        SetupSave(manager);

        var result = await CreateSource().RewrapAsync(Tenant, OldKeys, CancellationToken.None);

        Assert.Equal(new ProtectedValueRewrapResult(1, 0), result);
    }

    [Fact]
    public async Task Rewrap_AConflict_ReadsThePageAgainAndWritesIt()
    {
        var stale = Value("s1", OldKey);
        var (store1, manager1) = EnqueueManager();
        SetupPage(store1, null, null, stale);
        SetupUpdate(store1, stale);
        manager1
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Throws(new StoreConcurrencyException())
            .Verifiable();

        var again = Value("s1", OldKey);
        var (store2, manager2) = EnqueueManager();
        SetupPage(store2, null, null, again);
        SetupUpdate(store2, again);
        SetupSave(manager2);

        var result = await CreateSource().RewrapAsync(Tenant, OldKeys, CancellationToken.None);

        Assert.Equal(new ProtectedValueRewrapResult(1, 0), result);
    }

    [Fact]
    public async Task Rewrap_ConflictsOnEveryAttempt_CountsThePageFailedAndReadsTheNextPage()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var stale = Value("s1", OldKey);
            var (store, manager) = EnqueueManager();
            SetupPage(store, null, "next", stale);
            SetupUpdate(store, stale);
            manager
                .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Throws(new StoreConcurrencyException())
                .Verifiable();
        }

        var next = Value("s2", OldKey);
        var (nextStore, nextManager) = EnqueueManager();
        SetupPage(nextStore, "next", null, next);
        SetupUpdate(nextStore, next);
        SetupSave(nextManager);

        var result = await CreateSource().RewrapAsync(Tenant, OldKeys, CancellationToken.None);

        Assert.Equal(new ProtectedValueRewrapResult(1, 1), result);
    }

    [Fact]
    public async Task Rewrap_AMasterKeyThePageNamesIsGone_CountsThePageFailedAndReadsTheNextPage()
    {
        _provider.KeysMissing = true;
        var first = Value("s1", OldKey);
        var second = Value("s2", OldKey);
        var third = Value("s3", OldKey);
        var (store1, _) = EnqueueManager();
        SetupPage(store1, null, "next", first, second);
        var (store2, _) = EnqueueManager();
        SetupPage(store2, "next", null, third);

        var result = await CreateSource().RewrapAsync(Tenant, OldKeys, CancellationToken.None);

        Assert.Equal(new ProtectedValueRewrapResult(0, 3), result);
        Assert.Equal(2, _provider.Requests.Count);
    }

    [Fact]
    public async Task Rewrap_ThePlatformOwner_ReturnsNoneWithoutReading()
    {
        var result = await CreateSource()
            .RewrapAsync(SecretOwner.Platform, OldKeys, CancellationToken.None);

        Assert.Equal(ProtectedValueRewrapResult.None, result);
    }

    [Fact]
    public async Task Rewrap_NoKeys_ReadsNothing()
    {
        var result = await CreateSource()
            .RewrapAsync(Tenant, new HashSet<Guid>(), CancellationToken.None);

        Assert.Equal(ProtectedValueRewrapResult.None, result);
    }

    #endregion

    #region FindReferencedAsync Tests

    [Fact]
    public async Task FindReferenced_AValueNamesTheKey_ReportsIt()
    {
        var (store, _) = EnqueueManager();
        SetupPage(store, null, null, Value("s1", ActiveKey), Value("s2", OldKey));

        var found = await CreateSource()
            .FindReferencedAsync(Tenant, OldKeys, CancellationToken.None);

        Assert.Equal([OldKey], found);
    }

    [Fact]
    public async Task FindReferenced_NoValueNamesTheKeys_ReportsNone()
    {
        var (store, _) = EnqueueManager();
        SetupPage(store, null, null, Value("s1", ActiveKey));

        var found = await CreateSource()
            .FindReferencedAsync(Tenant, OldKeys, CancellationToken.None);

        Assert.Empty(found);
    }

    [Fact]
    public async Task FindReferenced_AValueOnALaterPageNamesTheKey_ReportsIt()
    {
        var (store1, _) = EnqueueManager();
        SetupPage(store1, null, "next", Value("s1", ActiveKey));
        var (store2, _) = EnqueueManager();
        SetupPage(store2, "next", null, Value("s2", OldKey));

        var found = await CreateSource()
            .FindReferencedAsync(Tenant, OldKeys, CancellationToken.None);

        Assert.Equal([OldKey], found);
    }

    [Fact]
    public async Task FindReferenced_StopsOnceEveryKeyIsFound()
    {
        var keys = new HashSet<Guid> { OldKey, OlderKey };
        // The first page holds both keys and has a next page, which is never requested: no manager is queued for it.
        var (store, _) = EnqueueManager();
        SetupPageFor(store, keys, null, "next", Value("s1", OldKey), Value("s2", OlderKey));

        var found = await CreateSource().FindReferencedAsync(Tenant, keys, CancellationToken.None);

        Assert.Equal(keys, found.ToHashSet());
        Assert.Empty(_storeManagers);
    }

    [Fact]
    public async Task FindReferenced_AnUnknownOwnerType_ReadsNothing()
    {
        var found = await CreateSource()
            .FindReferencedAsync(new SecretOwner("custom", "x"), OldKeys, CancellationToken.None);

        Assert.Empty(found);
    }

    #endregion
}
