// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Usage;

public sealed class TenantUsagePruneServiceTests : BaseTests
{
    private static DateTimeOffset Now { get; } = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static DateOnly Today { get; } = new(2026, 10, 21);
    private static TimeSpan PruneInterval { get; } = TimeSpan.FromHours(1);
    private static TimeSpan WaitTimeout { get; } = TimeSpan.FromSeconds(5);

    private FakeTimeProvider TimeProvider { get; } = new(Now);
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantUsageStore> MockTenantUsageStore { get; }
    private TenantUsagePruneService Service { get; }

    public TenantUsagePruneServiceTests()
    {
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTenantUsageStore = CreateStrictMock<ITenantUsageStore>();
        Service = new TenantUsagePruneService(
            MockStoreManagerFactory.Object,
            Options.Create(new TenantUsageOptions { Retention = TimeSpan.FromDays(10) }),
            TimeProvider,
            NullLogger<TenantUsagePruneService>.Instance
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Service.Dispose();
        }

        base.Dispose(disposing);
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

    private void SetupRemoveBefore(int removed, Times times, CancellationToken cancellationToken)
    {
        MockTenantUsageStore
            .Setup(x =>
                x.RemoveBeforeAsync(
                    Today.AddDays(-10),
                    TenantUsagePruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(removed)
            .Verifiable(times);
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenAPruneFails_PrunesAgainAtTheNextTick()
    {
        var timerCreated = new TaskCompletionSource<(TimerCallback Callback, object? State)>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var mockTimer = CreateStrictMock<ITimer>();
        mockTimer.Setup(x => x.Dispose()).Verifiable();
        var mockTimeProvider = CreateStrictMock<TimeProvider>();
        mockTimeProvider
            .Setup(x =>
                x.CreateTimer(
                    It.IsAny<TimerCallback>(),
                    It.IsAny<object?>(),
                    PruneInterval,
                    PruneInterval
                )
            )
            .Callback(
                (TimerCallback callback, object? state, TimeSpan _, TimeSpan _) =>
                    timerCreated.SetResult((callback, state))
            )
            .Returns(mockTimer.Object)
            .Verifiable();
        mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        SetupStores();
        MockTenantUsageStore
            .Setup(x =>
                x.RemoveBeforeAsync(
                    Today.AddDays(-10),
                    TenantUsagePruneService.BatchSize,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(3)
            .Verifiable(Times.Exactly(2));
        var firstSave = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var secondSave = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var saves = 0;
        var failure = new InvalidOperationException("unreachable");
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (++saves == 1)
                {
                    firstSave.SetResult();
                    return ValueTask.FromException(failure);
                }

                secondSave.SetResult();
                return ValueTask.CompletedTask;
            })
            .Verifiable(Times.Exactly(2));
        var options = new TenantUsageOptions
        {
            Retention = TimeSpan.FromDays(10),
            PruneInterval = PruneInterval,
        };
        using var service = new TenantUsagePruneService(
            MockStoreManagerFactory.Object,
            Options.Create(options),
            mockTimeProvider.Object,
            NullLogger<TenantUsagePruneService>.Instance
        );

        await service.StartAsync(CancellationToken.None);
        var (tick, state) = await timerCreated.Task.WaitAsync(WaitTimeout);
        tick(state);
        await firstSave.Task.WaitAsync(WaitTimeout);
        tick(state);
        await secondSave.Task.WaitAsync(WaitTimeout);
        await service.StopAsync(CancellationToken.None);

        Assert.NotNull(service.ExecuteTask);
        Assert.True(service.ExecuteTask.IsCompletedSuccessfully);
    }

    #endregion

    #region PruneAsync Tests

    [Fact]
    public async Task PruneAsync_RemovesTheDaysPastRetention()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        SetupRemoveBefore(3, Times.Once(), cancellationToken);
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
        SetupRemoveBefore(3, Times.Exactly(2), cancellationToken);
        MockStoreManager.SetupSaveChangesFailingOnce(
            new InvalidOperationException("unreachable"),
            Times.Exactly(2),
            cancellationToken
        );

        await Service.PruneAsync(cancellationToken);
        await Service.PruneAsync(cancellationToken);
    }

    [Fact]
    public async Task PruneAsync_WhenTheCommitTimesOut_RetriesOnTheNextTick()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        SetupRemoveBefore(3, Times.Exactly(2), cancellationToken);
        MockStoreManager.SetupSaveChangesFailingOnce(
            new TaskCanceledException("timed out"),
            Times.Exactly(2),
            cancellationToken
        );

        await Service.PruneAsync(cancellationToken);
        await Service.PruneAsync(cancellationToken);
    }

    [Fact]
    public async Task PruneAsync_WhenTheTokenIsCancelled_Throws()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        await cancellationTokenSource.CancelAsync();
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(cancellationToken))
            .ThrowsAsync(new OperationCanceledException(cancellationToken))
            .Verifiable();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Service.PruneAsync(cancellationToken)
        );
    }

    [Fact]
    public async Task PruneAsync_WhenABatchIsFull_RemovesTheNextBatch()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        var batches = 0;
        MockTenantUsageStore
            .Setup(x =>
                x.RemoveBeforeAsync(
                    Today.AddDays(-10),
                    TenantUsagePruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(() => ++batches == 1 ? TenantUsagePruneService.BatchSize : 5)
            .Verifiable(Times.Exactly(2));
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
        SetupRemoveBefore(3, Times.Exactly(2), cancellationToken);
        MockStoreManager.SetupSaveChangesFailingOnce(
            new StoreConcurrencyException("conflict"),
            Times.Exactly(2),
            cancellationToken
        );

        await Service.PruneAsync(cancellationToken);
    }

    #endregion
}
