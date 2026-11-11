// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Persistence;

public sealed class GrantPruneServiceTests : IDisposable
{
    private static DateTimeOffset Now { get; } = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static TimeSpan PruneInterval { get; } = TimeSpan.FromHours(1);
    private static TimeSpan WaitTimeout { get; } = TimeSpan.FromSeconds(5);

    private MockRepository MockRepository { get; }
    private FakeTimeProvider TimeProvider { get; } = new(Now);
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IGrantStore> MockGrantStore { get; }
    private GrantPruneService Service { get; }

    public GrantPruneServiceTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockGrantStore = MockRepository.Create<IGrantStore>();
        Service = new GrantPruneService(
            MockStoreManagerFactory.Object,
            Options.Create(new GrantRetentionOptions()),
            TimeProvider,
            NullLogger<GrantPruneService>.Instance
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
            .Setup(x => x.GetStore<IGrantStore>())
            .Returns(MockGrantStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenAPruneFails_PrunesAgainAtTheNextTick()
    {
        var timerCreated = new TaskCompletionSource<(TimerCallback Callback, object? State)>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var mockTimer = MockRepository.Create<ITimer>();
        mockTimer.Setup(x => x.Dispose()).Verifiable();
        var mockTimeProvider = MockRepository.Create<TimeProvider>();
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
        MockGrantStore
            .Setup(x =>
                x.RemoveEndedBeforeAsync(
                    Now - TimeSpan.FromDays(14),
                    GrantPruneService.GrantTypesEndedByRevocation,
                    GrantPruneService.BatchSize,
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
        var options = new GrantRetentionOptions { PruneInterval = PruneInterval };
        using var service = new GrantPruneService(
            MockStoreManagerFactory.Object,
            Options.Create(options),
            mockTimeProvider.Object,
            NullLogger<GrantPruneService>.Instance
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
    public async Task PruneAsync_RemovesTheGrantsThatEndedBeforeTheRetention()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        MockGrantStore
            .Setup(x =>
                x.RemoveEndedBeforeAsync(
                    Now - TimeSpan.FromDays(14),
                    GrantPruneService.GrantTypesEndedByRevocation,
                    GrantPruneService.BatchSize,
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
    public async Task PruneAsync_WhenABatchIsFull_RemovesTheNextBatch()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        var batches = 0;
        MockGrantStore
            .Setup(x =>
                x.RemoveEndedBeforeAsync(
                    Now - TimeSpan.FromDays(14),
                    GrantPruneService.GrantTypesEndedByRevocation,
                    GrantPruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(() => ++batches == 1 ? GrantPruneService.BatchSize : 5)
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
        MockGrantStore
            .Setup(x =>
                x.RemoveEndedBeforeAsync(
                    Now - TimeSpan.FromDays(14),
                    GrantPruneService.GrantTypesEndedByRevocation,
                    GrantPruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(3)
            .Verifiable(Times.Exactly(2));
        MockStoreManager.SetupSaveChangesFailingOnce(
            new StoreConcurrencyException("conflict"),
            Times.Exactly(2),
            cancellationToken
        );

        await Service.PruneAsync(cancellationToken);
    }

    [Fact]
    public async Task PruneAsync_WhenTheCommitFails_LogsAndRetriesOnTheNextTick()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        MockGrantStore
            .Setup(x =>
                x.RemoveEndedBeforeAsync(
                    Now - TimeSpan.FromDays(14),
                    GrantPruneService.GrantTypesEndedByRevocation,
                    GrantPruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(3)
            .Verifiable(Times.Exactly(2));
        MockStoreManager.SetupSaveChangesFailingOnce(
            new InvalidOperationException("unreachable"),
            Times.Exactly(2),
            cancellationToken
        );

        await Service.PruneAsync(cancellationToken);
        await Service.PruneAsync(cancellationToken);
    }

    [Fact]
    public async Task PruneAsync_WhenTheCommitTimesOut_LogsAndRetriesOnTheNextTick()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        MockGrantStore
            .Setup(x =>
                x.RemoveEndedBeforeAsync(
                    Now - TimeSpan.FromDays(14),
                    GrantPruneService.GrantTypesEndedByRevocation,
                    GrantPruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(3)
            .Verifiable(Times.Exactly(2));
        MockStoreManager.SetupSaveChangesFailingOnce(
            new TaskCanceledException("timed out"),
            Times.Exactly(2),
            cancellationToken
        );

        await Service.PruneAsync(cancellationToken);
        await Service.PruneAsync(cancellationToken);
    }

    #endregion

    #region GrantTypesEndedByRevocation Property Tests

    [Fact]
    public void GrantTypesEndedByRevocation_Always_IsTheSessionGrantType()
    {
        Assert.Equal<string>(
            [OpenIdConstants.PersistedGrantTypes.Session],
            GrantPruneService.GrantTypesEndedByRevocation
        );
    }

    #endregion
}
