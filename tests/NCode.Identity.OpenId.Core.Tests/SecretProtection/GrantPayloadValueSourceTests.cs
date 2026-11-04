// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.SecretProtection;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.SecretProtection;

public sealed class GrantPayloadValueSourceTests : IDisposable
{
    private static readonly SecretOwner Tenant = SecretOwner.ForTenant("tenant-1");
    private static readonly Guid OldKey = Guid.NewGuid();
    private static readonly Guid ActiveKey = Guid.NewGuid();

    private readonly MockRepository _mocks = new(MockBehavior.Strict);
    private readonly Mock<IStoreManagerFactory> _storeManagerFactory;
    private readonly Queue<IStoreManager> _storeManagers = new();
    private readonly FakeRewrapProvider _provider = new(ActiveKey);

    public GrantPayloadValueSourceTests()
    {
        _storeManagerFactory = _mocks.Create<IStoreManagerFactory>();
        _storeManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .Returns(() => new ValueTask<IStoreManager>(_storeManagers.Dequeue()));
    }

    public void Dispose()
    {
        _mocks.Verify();
        Assert.Empty(_storeManagers);
    }

    private GrantPayloadValueSource CreateSource() =>
        new(_storeManagerFactory.Object, _provider, NullLogger<GrantPayloadValueSource>.Instance);

    private (Mock<IGrantStore> Store, Mock<IStoreManager> Manager) EnqueueManager()
    {
        var store = _mocks.Create<IGrantStore>();
        var manager = _mocks.Create<IStoreManager>();
        manager.Setup(x => x.GetStore<IGrantStore>()).Returns(store.Object);
        manager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
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
    ) =>
        store
            .Setup(x =>
                x.GetProtectedPayloadPageAsync(
                    Tenant.OwnerId,
                    It.Is<PageRequest>(page => page.Cursor == cursor && page.Limit == 100),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(new ValueTask<PagedResult<PersistedProtectedValue>>(Page(next, items)));

    #region RewrapAsync Tests

    [Fact]
    public async Task Rewrap_RewrapsOnlyTheValuesThatNameTheKeyWithOneProtectorPerPage()
    {
        var stale1 = Value("s1", OldKey);
        var current = Value("s2", ActiveKey);
        var stale2 = Value("s3", OldKey);
        var (store, manager) = EnqueueManager();
        SetupPage(store, null, null, stale1, current, stale2);
        store
            .Setup(x =>
                x.UpdateProtectedPayloadAsync(
                    It.Is<PersistedProtectedValue>(v => v.RecordId == "s1" || v.RecordId == "s3"),
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask);
        manager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var count = await CreateSource().RewrapAsync(Tenant, OldKey, CancellationToken.None);

        Assert.Equal(2, count);
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
    public async Task Rewrap_ReadsEveryPage()
    {
        var first = Value("s1", OldKey);
        var second = Value("s2", OldKey);
        var (store1, manager1) = EnqueueManager();
        SetupPage(store1, null, "next", first);
        store1
            .Setup(x => x.UpdateProtectedPayloadAsync(first, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        manager1
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        var (store2, manager2) = EnqueueManager();
        SetupPage(store2, "next", null, second);
        store2
            .Setup(x => x.UpdateProtectedPayloadAsync(second, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        manager2
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var count = await CreateSource().RewrapAsync(Tenant, OldKey, CancellationToken.None);

        Assert.Equal(2, count);
        Assert.Equal(2, _provider.Requests.Count);
    }

    [Fact]
    public async Task Rewrap_AConflict_ReadsThePageAgainAndWritesIt()
    {
        var stale = Value("s1", OldKey);
        var (store1, manager1) = EnqueueManager();
        SetupPage(store1, null, null, stale);
        store1
            .Setup(x => x.UpdateProtectedPayloadAsync(stale, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        manager1
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Throws(new StoreConcurrencyException());

        var again = Value("s1", OldKey);
        var (store2, manager2) = EnqueueManager();
        SetupPage(store2, null, null, again);
        store2
            .Setup(x => x.UpdateProtectedPayloadAsync(again, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);
        manager2
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var count = await CreateSource().RewrapAsync(Tenant, OldKey, CancellationToken.None);

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task Rewrap_ConflictsOnEveryAttempt_GivesUpAfterThree()
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var stale = Value("s1", OldKey);
            var (store, manager) = EnqueueManager();
            SetupPage(store, null, null, stale);
            store
                .Setup(x => x.UpdateProtectedPayloadAsync(stale, It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
            manager
                .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Throws(new StoreConcurrencyException());
        }

        await Assert.ThrowsAsync<StoreConcurrencyException>(async () =>
            await CreateSource().RewrapAsync(Tenant, OldKey, CancellationToken.None)
        );
    }

    [Fact]
    public async Task Rewrap_TheServerOwner_ReturnsZeroWithoutReading()
    {
        var count = await CreateSource()
            .RewrapAsync(SecretOwner.ForServer("server-1"), OldKey, CancellationToken.None);

        Assert.Equal(0, count);
    }

    #endregion

    #region ReferencesAsync Tests

    [Fact]
    public async Task References_AValueNamesTheKey_ReturnsTrue()
    {
        var (store, _) = EnqueueManager();
        SetupPage(store, null, null, Value("s1", ActiveKey), Value("s2", OldKey));

        Assert.True(await CreateSource().ReferencesAsync(Tenant, OldKey, CancellationToken.None));
    }

    [Fact]
    public async Task References_NoValueNamesTheKey_ReturnsFalse()
    {
        var (store, _) = EnqueueManager();
        SetupPage(store, null, null, Value("s1", ActiveKey));

        Assert.False(await CreateSource().ReferencesAsync(Tenant, OldKey, CancellationToken.None));
    }

    [Fact]
    public async Task References_AnUnknownOwnerType_ReturnsFalse()
    {
        Assert.False(
            await CreateSource()
                .ReferencesAsync(new SecretOwner("custom", "x"), OldKey, CancellationToken.None)
        );
    }

    #endregion
}
