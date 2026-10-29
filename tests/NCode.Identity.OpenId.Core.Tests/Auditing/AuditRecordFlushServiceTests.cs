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
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Json;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public sealed class AuditRecordFlushServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private MockRepository MockRepository { get; }
    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IAuditRecordStore> MockAuditRecordStore { get; }
    private AuditRecordQueue Queue { get; }
    private AuditRecordFlushService Service { get; }

    public AuditRecordFlushServiceTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockTimeProvider = MockRepository.Create<TimeProvider>();
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockAuditRecordStore = MockRepository.Create<IAuditRecordStore>();
        var options = Options.Create(
            new AuditRecordOptions { Retention = TimeSpan.FromDays(90), QueueCapacity = 1000 }
        );
        Queue = new AuditRecordQueue(options);
        Service = new AuditRecordFlushService(
            Queue,
            MockStoreManagerFactory.Object,
            options,
            MockTimeProvider.Object,
            NullLogger<AuditRecordFlushService>.Instance
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
            .Setup(x => x.GetStore<IAuditRecordStore>())
            .Returns(MockAuditRecordStore.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask).Verifiable();
    }

    private List<PersistedAuditRecord> Enqueue(int count)
    {
        var records = Enumerable
            .Range(0, count)
            .Select(_ => new PersistedAuditRecord
            {
                EventId = Guid.CreateVersion7(),
                EventTime = Now,
                EventName = "token.issued",
                EventCategory = "token",
                Outcome = "success",
                Severity = "info",
                ActorType = "client",
                Resources = [],
                Record = JsonElements.EmptyObject,
            })
            .ToList();
        foreach (var record in records)
            Queue.TryEnqueue(record);
        return records;
    }

    #region FlushAsync Tests

    [Fact]
    public async Task FlushAsync_WhenNothingIsQueued_DoesNotOpenTheStore()
    {
        await Service.FlushAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FlushAsync_WritesTheQueuedRecordsInBatches()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        var records = Enqueue(AuditRecordFlushService.BatchSize + 3);
        var written = new List<int>();
        MockAuditRecordStore
            .Setup(x =>
                x.AddAsync(It.IsAny<IReadOnlyCollection<PersistedAuditRecord>>(), cancellationToken)
            )
            .Callback<IReadOnlyCollection<PersistedAuditRecord>, CancellationToken>(
                (batch, _) => written.Add(batch.Count)
            )
            .ReturnsAsync(
                (IReadOnlyCollection<PersistedAuditRecord> batch, CancellationToken _) =>
                    batch.Count
            )
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Service.FlushAsync(cancellationToken);

        Assert.Equal([AuditRecordFlushService.BatchSize, 3], written);
        Assert.Equal(0, Queue.Count);
        Assert.Equal(records.Count, written.Sum());
    }

    [Fact]
    public async Task FlushAsync_WhenAWriteFails_HoldsTheBatchAndWritesItFirstNextTime()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        var first = Enqueue(2);
        var batches = new List<Guid[]>();
        MockAuditRecordStore
            .Setup(x =>
                x.AddAsync(It.IsAny<IReadOnlyCollection<PersistedAuditRecord>>(), cancellationToken)
            )
            .Callback<IReadOnlyCollection<PersistedAuditRecord>, CancellationToken>(
                (batch, _) => batches.Add([.. batch.Select(record => record.EventId)])
            )
            .ReturnsAsync(2)
            .Verifiable();
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new InvalidOperationException("The database is unreachable."))
            .Returns(ValueTask.CompletedTask);

        await Service.FlushAsync(cancellationToken);
        var second = Enqueue(1);
        await Service.FlushAsync(cancellationToken);

        Assert.Equal(2, batches.Count);
        Assert.Equal(first.Select(record => record.EventId), batches[0]);
        Assert.Equal(first.Concat(second).Select(record => record.EventId), batches[1]);
        Assert.Equal(0, Queue.Count);
    }

    #endregion

    #region PruneAsync Tests

    [Fact]
    public async Task PruneAsync_RemovesUntilABatchIsShortAndThenWaitsAnHour()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        MockTimeProvider.SetupSequence(x => x.GetUtcNow()).Returns(Now).Returns(Now.AddMinutes(59));
        MockAuditRecordStore
            .SetupSequence(x =>
                x.RemoveBeforeAsync(
                    Now.AddDays(-90),
                    AuditRecordFlushService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(AuditRecordFlushService.BatchSize)
            .ReturnsAsync(3);
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Service.PruneAsync(cancellationToken);
        await Service.PruneAsync(cancellationToken);

        MockAuditRecordStore.Verify(
            x =>
                x.RemoveBeforeAsync(It.IsAny<DateTimeOffset>(), It.IsAny<int>(), cancellationToken),
            Times.Exactly(2)
        );
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_CalledTwiceConcurrently_FlushesOnce()
    {
        var cancellationToken = CancellationToken.None;
        var save = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        SetupStores();
        Enqueue(1);
        MockAuditRecordStore
            .Setup(x =>
                x.AddAsync(It.IsAny<IReadOnlyCollection<PersistedAuditRecord>>(), cancellationToken)
            )
            .ReturnsAsync(1)
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
        Assert.Equal(0, Queue.Count);
    }

    [Fact]
    public async Task StopAsync_WhenTheStopTokenEndsTheWait_LogsTheHeldRecords()
    {
        var cancellationToken = CancellationToken.None;
        var save = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockLogger = MockRepository.Create<ILogger<AuditRecordFlushService>>();
        mockLogger.Setup(x => x.IsEnabled(LogLevel.Warning)).Returns(true).Verifiable();
        mockLogger
            .Setup(x =>
                x.Log(
                    LogLevel.Warning,
                    It.Is<EventId>(eventId => eventId.Id == EventIds.AuditRecordsDiscardedOnStop),
                    It.Is<It.IsAnyType>(
                        (state, _) =>
                            $"{state}".StartsWith("3 audit records ", StringComparison.Ordinal)
                    ),
                    It.IsAny<Exception?>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()
                )
            )
            .Verifiable(Times.Once);
        using var service = new AuditRecordFlushService(
            Queue,
            MockStoreManagerFactory.Object,
            Options.Create(new AuditRecordOptions { QueueCapacity = 1000 }),
            MockTimeProvider.Object,
            mockLogger.Object
        );
        SetupStores();
        Enqueue(2);
        var written = new List<int>();
        MockAuditRecordStore
            .Setup(x =>
                x.AddAsync(It.IsAny<IReadOnlyCollection<PersistedAuditRecord>>(), cancellationToken)
            )
            .Callback<IReadOnlyCollection<PersistedAuditRecord>, CancellationToken>(
                (batch, _) => written.Add(batch.Count)
            )
            .ReturnsAsync(1)
            .Verifiable(Times.Exactly(2));
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .Returns(new ValueTask(save.Task))
            .Verifiable(Times.Exactly(2));
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var flush = service.FlushAsync(cancellationToken).AsTask();
        Enqueue(1);

        await service.StopAsync(cancellation.Token);

        save.SetResult();
        await flush;

        Assert.Equal([2, 1], written);
    }

    #endregion
}
