// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Usage;

public sealed class TenantUsagePruneServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 21);

    private MockRepository MockRepository { get; }
    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantUsageStore> MockTenantUsageStore { get; }
    private TenantUsagePruneService Service { get; }

    public TenantUsagePruneServiceTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockTimeProvider = MockRepository.Create<TimeProvider>();
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTenantUsageStore = MockRepository.Create<ITenantUsageStore>();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now);
        Service = new TenantUsagePruneService(
            MockStoreManagerFactory.Object,
            Options.Create(new TenantUsageOptions { Retention = TimeSpan.FromDays(10) }),
            MockTimeProvider.Object,
            NullLogger<TenantUsagePruneService>.Instance
        );
    }

    public void Dispose()
    {
        Service.Dispose();
        MockRepository.Verify();
    }

    private void SetupStores()
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.GetStore<ITenantUsageStore>())
            .Returns(MockTenantUsageStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    #region PruneAsync Tests

    [Fact]
    public async Task PruneAsync_RemovesTheDaysPastRetention()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        MockTenantUsageStore
            .Setup(x =>
                x.RemoveBeforeAsync(
                    Today.AddDays(-10),
                    TenantUsagePruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(3)
            .Verifiable(Times.Once);
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);

        await Service.PruneAsync(cancellationToken);
    }

    [Fact]
    public async Task PruneAsync_WhenTheCommitFails_RetriesOnTheNextTick()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        MockTenantUsageStore
            .Setup(x =>
                x.RemoveBeforeAsync(
                    Today.AddDays(-10),
                    TenantUsagePruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(3)
            .Verifiable(Times.Exactly(2));
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new InvalidOperationException("unreachable"))
            .Returns(ValueTask.CompletedTask);

        await Service.PruneAsync(cancellationToken);
        await Service.PruneAsync(cancellationToken);
    }

    [Fact]
    public async Task PruneAsync_WhenABatchIsFull_RemovesTheNextBatch()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        MockTenantUsageStore
            .SetupSequence(x =>
                x.RemoveBeforeAsync(
                    Today.AddDays(-10),
                    TenantUsagePruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(TenantUsagePruneService.BatchSize)
            .ReturnsAsync(5);
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));

        await Service.PruneAsync(cancellationToken);
    }

    [Fact]
    public async Task PruneAsync_WhenAnotherReplicaRemovedTheBatchFirst_ReadsTheNextBatch()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        MockTenantUsageStore
            .Setup(x =>
                x.RemoveBeforeAsync(
                    Today.AddDays(-10),
                    TenantUsagePruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(3)
            .Verifiable(Times.Exactly(2));
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new StoreConcurrencyException("conflict"))
            .Returns(ValueTask.CompletedTask);

        await Service.PruneAsync(cancellationToken);
    }

    #endregion
}
