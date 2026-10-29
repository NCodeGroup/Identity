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

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
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
                Record = "{}",
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
}
