#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Usage;

public sealed class TenantUsageFlushServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 21);

    private MockRepository MockRepository { get; }
    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITenantUsageStore> MockTenantUsageStore { get; }
    private DefaultTenantUsageRecorder Recorder { get; }
    private TenantUsageFlushService Service { get; }

    public TenantUsageFlushServiceTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockTimeProvider = MockRepository.Create<TimeProvider>();
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockTenantUsageStore = MockRepository.Create<ITenantUsageStore>();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now);
        Recorder = new DefaultTenantUsageRecorder(MockTimeProvider.Object);
        Service = new TenantUsageFlushService(
            Recorder,
            MockStoreManagerFactory.Object,
            Options.Create(new TenantUsageOptions { Retention = TimeSpan.FromDays(10) }),
            MockTimeProvider.Object,
            NullLogger<TenantUsageFlushService>.Instance
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

    private static IReadOnlyCollection<PersistedTenantUsage> IsUsage(
        string tenantId,
        string meter,
        long count
    ) =>
        It.Is<IReadOnlyCollection<PersistedTenantUsage>>(usage =>
            usage.Count == 1
            && usage.Single().TenantId == tenantId
            && usage.Single().Day == Today
            && usage.Single().Meter == meter
            && usage.Single().Count == count
        );

    #region FlushAsync Tests

    [Fact]
    public async Task FlushAsync_WhenNothingWasRecorded_DoesNotOpenTheStore()
    {
        await Service.FlushAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FlushAsync_AddsTheRecordedUsageAsTheReplicasFirstBatch()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        MockTenantUsageStore
            .Setup(x =>
                x.AddAsync(
                    Service.ReplicaId,
                    1,
                    IsUsage("tenant-1", TenantUsageMeters.Requests, 2),
                    cancellationToken
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Service.FlushAsync(cancellationToken);

        Assert.Empty(Recorder.Drain());
    }

    [Fact]
    public async Task FlushAsync_WhenTheCommitFails_RetriesTheBatchUnderItsNumber()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        MockTenantUsageStore
            .Setup(x =>
                x.AddAsync(
                    Service.ReplicaId,
                    1,
                    IsUsage("tenant-1", TenantUsageMeters.Requests, 1),
                    cancellationToken
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new InvalidOperationException("conflict"))
            .Returns(ValueTask.CompletedTask);

        await Service.FlushAsync(cancellationToken);
        await Service.FlushAsync(cancellationToken);

        Assert.Empty(Recorder.Drain());
    }

    [Fact]
    public async Task FlushAsync_WhileABatchIsHeld_LeavesNewUsageInTheRecorder()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        MockTenantUsageStore
            .Setup(x =>
                x.AddAsync(
                    Service.ReplicaId,
                    1,
                    IsUsage("tenant-1", TenantUsageMeters.Requests, 1),
                    cancellationToken
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new InvalidOperationException("unreachable"))
            .Verifiable(Times.Exactly(2));

        await Service.FlushAsync(cancellationToken);
        Recorder.Record("tenant-2", TenantUsageMeters.Requests);
        await Service.FlushAsync(cancellationToken);

        Assert.Equal("tenant-2", Assert.Single(Recorder.Drain()).Key.TenantId);
    }

    [Fact]
    public async Task FlushAsync_WhenABatchFails_DoesNotAddTheBatchesAfterIt()
    {
        var cancellationToken = CancellationToken.None;
        var numbers = new List<long>();
        SetupStores();
        for (var index = 0; index <= TenantUsageFlushService.BatchSize; ++index)
        {
            Recorder.Record($"tenant-{index:D4}", TenantUsageMeters.Requests);
        }

        MockTenantUsageStore
            .Setup(x =>
                x.AddAsync(
                    Service.ReplicaId,
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<PersistedTenantUsage>>(),
                    cancellationToken
                )
            )
            .Callback(
                (
                    Guid _,
                    long number,
                    IReadOnlyCollection<PersistedTenantUsage> _,
                    CancellationToken _
                ) => numbers.Add(number)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new InvalidOperationException("conflict"))
            .Verifiable(Times.Once);

        await Service.FlushAsync(cancellationToken);

        Assert.Equal([1L], numbers);
    }

    [Fact]
    public async Task FlushAsync_WhenAHeldBatchIsAdded_AddsTheBatchesAfterItInOrder()
    {
        var cancellationToken = CancellationToken.None;
        var numbers = new List<long>();
        SetupStores();
        for (var index = 0; index <= TenantUsageFlushService.BatchSize; ++index)
        {
            Recorder.Record($"tenant-{index:D4}", TenantUsageMeters.Requests);
        }

        MockTenantUsageStore
            .Setup(x =>
                x.AddAsync(
                    Service.ReplicaId,
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<PersistedTenantUsage>>(),
                    cancellationToken
                )
            )
            .Callback(
                (
                    Guid _,
                    long number,
                    IReadOnlyCollection<PersistedTenantUsage> _,
                    CancellationToken _
                ) => numbers.Add(number)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(3));
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new InvalidOperationException("conflict"))
            .Returns(ValueTask.CompletedTask)
            .Returns(ValueTask.CompletedTask);

        await Service.FlushAsync(cancellationToken);
        await Service.FlushAsync(cancellationToken);

        Assert.Equal([1L, 1L, 2L], numbers);
        Assert.Empty(Recorder.Drain());
    }

    #endregion

    #region PruneAsync Tests

    [Fact]
    public async Task PruneAsync_RemovesTheDaysPastRetentionOncePerDay()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        MockTenantUsageStore
            .Setup(x =>
                x.RemoveBeforeAsync(
                    Today.AddDays(-10),
                    TenantUsageFlushService.BatchSize,
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
                    TenantUsageFlushService.BatchSize,
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
                    TenantUsageFlushService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(TenantUsageFlushService.BatchSize)
            .ReturnsAsync(5);
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));

        await Service.PruneAsync(cancellationToken);
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
                    TenantUsageFlushService.BatchSize,
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

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_WhenCalledTwice_RunsOneFinalFlush()
    {
        var cancellationToken = CancellationToken.None;
        var save = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetupStores();
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        MockTenantUsageStore
            .Setup(x =>
                x.AddAsync(
                    Service.ReplicaId,
                    1,
                    IsUsage("tenant-1", TenantUsageMeters.Requests, 1),
                    cancellationToken
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .Returns(new ValueTask(save.Task))
            .Verifiable(Times.Once);

        var first = Service.StopAsync(cancellationToken);
        var second = Service.StopAsync(cancellationToken);
        save.SetResult();
        await Task.WhenAll(first, second);

        Assert.Same(first, second);
    }

    [Fact]
    public async Task StopAsync_WhenTheFinalFlushFails_LogsTheUsageAsDiscardedWithoutThrowing()
    {
        var cancellationToken = CancellationToken.None;
        var mockLogger = MockRepository.Create<ILogger<TenantUsageFlushService>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true).Verifiable();
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(eventId => eventId.Id == EventIds.TenantUsageFlushFailed),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable(Times.Once);
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(eventId => eventId.Id == EventIds.TenantUsageDiscardedOnStop),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable(Times.Once);
        using var service = new TenantUsageFlushService(
            Recorder,
            MockStoreManagerFactory.Object,
            Options.Create(new TenantUsageOptions()),
            MockTimeProvider.Object,
            mockLogger.Object
        );
        SetupStores();
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        MockTenantUsageStore
            .Setup(x =>
                x.AddAsync(
                    service.ReplicaId,
                    1,
                    IsUsage("tenant-1", TenantUsageMeters.Requests, 1),
                    cancellationToken
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new InvalidOperationException("unreachable"))
            .Verifiable(Times.Once);

        await service.StopAsync(cancellationToken);

        Assert.Empty(Recorder.Drain());
    }

    [Fact]
    public async Task StopAsync_WhileAFlushRuns_WaitsForItAndAddsTheUsageAfterIt()
    {
        var cancellationToken = CancellationToken.None;
        var numbers = new List<long>();
        var firstAdd = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetupStores();
        MockTenantUsageStore
            .Setup(x =>
                x.AddAsync(
                    Service.ReplicaId,
                    It.IsAny<long>(),
                    It.IsAny<IReadOnlyCollection<PersistedTenantUsage>>(),
                    cancellationToken
                )
            )
            .Returns(
                (
                    Guid _,
                    long number,
                    IReadOnlyCollection<PersistedTenantUsage> _,
                    CancellationToken _
                ) =>
                {
                    numbers.Add(number);
                    return number == 1 ? new ValueTask(firstAdd.Task) : ValueTask.CompletedTask;
                }
            )
            .Verifiable(Times.Exactly(2));
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Exactly(2));
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);

        var flush = Service.FlushAsync(cancellationToken).AsTask();
        Recorder.Record("tenant-2", TenantUsageMeters.Requests);
        var stop = Service.StopAsync(cancellationToken);

        Assert.False(stop.IsCompleted);

        firstAdd.SetResult();
        await flush;
        await stop;

        Assert.Equal([1L, 2L], numbers);
    }

    [Fact]
    public async Task StopAsync_WhenTheStopTokenIsCancelledBeforeTheGate_LogsTheUsageAsDiscardedWithoutFlushing()
    {
        var mockLogger = MockRepository.Create<ILogger<TenantUsageFlushService>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true).Verifiable();
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(eventId => eventId.Id == EventIds.TenantUsageDiscardedOnStop),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable(Times.Once);
        using var service = new TenantUsageFlushService(
            Recorder,
            MockStoreManagerFactory.Object,
            Options.Create(new TenantUsageOptions()),
            MockTimeProvider.Object,
            mockLogger.Object
        );
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await service.StopAsync(cancellation.Token);

        Assert.Empty(Recorder.Drain());
    }

    #endregion
}
