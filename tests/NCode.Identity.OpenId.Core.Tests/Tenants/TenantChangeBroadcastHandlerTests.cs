// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence.Stores;
using NCode.Identity.Secrets.Persistence.Protection;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tenants;

public sealed class TenantChangeBroadcastHandlerTests : IDisposable
{
    private const string TenantId = "acme";

    private MockRepository Mocks { get; } = new(MockBehavior.Strict);
    private Mock<IOpenIdTenantCache> MockTenantCache { get; }
    private Mock<ISecretProtectorProvider> MockSecretProtectorProvider { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantStore> MockTenantStore { get; }
    private TenantChangeBroadcastHandler Handler { get; }

    public TenantChangeBroadcastHandlerTests()
    {
        MockTenantCache = Mocks.Create<IOpenIdTenantCache>();
        MockSecretProtectorProvider = Mocks.Create<ISecretProtectorProvider>();
        MockStoreManagerFactory = Mocks.Create<IStoreManagerFactory>();
        MockStoreManager = Mocks.Create<IStoreManager>();
        MockTenantStore = Mocks.Create<ITenantStore>();
        Handler = new TenantChangeBroadcastHandler(
            MockTenantCache.Object,
            MockSecretProtectorProvider.Object,
            MockStoreManagerFactory.Object,
            NullLogger<TenantChangeBroadcastHandler>.Instance
        );
    }

    public void Dispose()
    {
        Mocks.Verify();
    }

    #region Channel Tests

    [Fact]
    public void Channel_IsTheTenantChangedChannel()
    {
        Assert.Equal("tenant-changed", Handler.Channel);
        Assert.Equal(TenantChangeBroadcastHandler.ChannelName, Handler.Channel);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_EvictsTheAnnouncedTenant()
    {
        MockSecretProtectorProvider
            .Setup(x => x.Evict(SecretOwner.ForTenant(TenantId)))
            .Verifiable();
        MockTenantCache
            .Setup(x =>
                x.RemoveAsync(TenantId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(TenantId, CancellationToken.None);
    }

    #endregion

    #region HandleMissedMessagesAsync Tests

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenNothingIsCached_ReadsNoStore()
    {
        SetupRevisions([]);

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_EvictsOnlyTheTenantsWhoseRowChangedOrIsGone()
    {
        SetupRevisions([
            new TenantRevision("acme", "a1"),
            new TenantRevision("globex", "g1"),
            new TenantRevision("initech", "i1"),
        ]);
        SetupStore(
            ids => Assert.Equal(["acme", "globex", "initech"], ids),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["acme"] = "a1",
                ["globex"] = "g2",
            }
        );
        SetupEviction("globex");
        SetupEviction("initech");

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_ReadsTheTokensInBatches()
    {
        var revisions = Enumerable
            .Range(0, TenantChangeBroadcastHandler.TenantBatchSize + 1)
            .Select(index => new TenantRevision($"tenant-{index}", "t1"))
            .ToList();
        SetupRevisions(revisions);
        var batchSizes = new List<int>();

        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockTenantStore
            .Setup(x =>
                x.GetConcurrencyTokensAsync(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (IReadOnlyCollection<string> ids, CancellationToken _) => batchSizes.Add(ids.Count)
            )
            .ReturnsAsync(
                (IReadOnlyCollection<string> ids, CancellationToken _) =>
                    (IReadOnlyDictionary<string, string>)
                        ids.ToDictionary(id => id, _ => "t1", StringComparer.OrdinalIgnoreCase)
            )
            .Verifiable();

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);

        Assert.Equal([TenantChangeBroadcastHandler.TenantBatchSize, 1], batchSizes);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenTheTokensCannotBeRead_EvictsEveryMasterKeyAndClearsTheCache()
    {
        SetupRevisions([new TenantRevision("acme", "a1")]);
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The store is unavailable."))
            .Verifiable();
        MockSecretProtectorProvider.Setup(x => x.EvictAll()).Verifiable();
        SetupClear();

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenReadingTheTokensTimesOut_EvictsEveryMasterKeyAndClearsTheCache()
    {
        SetupRevisions([new TenantRevision("acme", "a1")]);
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException())
            .Verifiable();
        MockSecretProtectorProvider.Setup(x => x.EvictAll()).Verifiable();
        SetupClear();

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenAnEvictionFails_EvictsTheOtherStaleTenantsAndClearsTheCache()
    {
        SetupRevisions([new TenantRevision("acme", "a1"), new TenantRevision("globex", "g1")]);
        SetupStore(
            ids => Assert.Equal(["acme", "globex"], ids),
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["acme"] = "a2",
                ["globex"] = "g2",
            }
        );
        MockSecretProtectorProvider.Setup(x => x.Evict(SecretOwner.ForTenant("acme"))).Verifiable();
        MockTenantCache
            .Setup(x =>
                x.RemoveAsync("acme", It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(() => ValueTask.FromException(new InvalidOperationException("Failed.")))
            .Verifiable();
        SetupEviction("globex");
        MockSecretProtectorProvider.Setup(x => x.EvictAll()).Verifiable();
        SetupClear();

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenTheTokensCannotBeReadAndTheClearFails_LogsAndCompletes()
    {
        SetupRevisions([new TenantRevision("acme", "a1")]);
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The store is unavailable."))
            .Verifiable();
        MockSecretProtectorProvider.Setup(x => x.EvictAll()).Verifiable();
        MockTenantCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The clear failed."))
            .Verifiable();

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    #endregion

    private void SetupRevisions(IReadOnlyCollection<TenantRevision> revisions) =>
        MockTenantCache
            .Setup(x =>
                x.GetRevisionsAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(revisions)
            .Verifiable();

    private void SetupStore(
        Action<IReadOnlyCollection<string>> inspectIds,
        IReadOnlyDictionary<string, string> tokens
    )
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantStore>())
            .Returns(MockTenantStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
        MockTenantStore
            .Setup(x =>
                x.GetConcurrencyTokensAsync(
                    It.IsAny<IReadOnlyCollection<string>>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(
                (IReadOnlyCollection<string> ids, CancellationToken _) =>
                    inspectIds(ids.Order(StringComparer.OrdinalIgnoreCase).ToList())
            )
            .ReturnsAsync(tokens)
            .Verifiable();
    }

    private void SetupEviction(string tenantId)
    {
        MockSecretProtectorProvider
            .Setup(x => x.Evict(SecretOwner.ForTenant(tenantId)))
            .Verifiable();
        MockTenantCache
            .Setup(x =>
                x.RemoveAsync(tenantId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    private void SetupClear() =>
        MockTenantCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
}
