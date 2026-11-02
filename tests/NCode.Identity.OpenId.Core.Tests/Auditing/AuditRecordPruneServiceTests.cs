// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Auditing;

public sealed class AuditRecordPruneServiceTests : BaseTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IAuditRecordStore> MockAuditRecordStore { get; }
    private AuditRecordPruneService Service { get; }

    public AuditRecordPruneServiceTests()
    {
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockAuditRecordStore = CreateStrictMock<IAuditRecordStore>();
        var options = new AuditRecordOptions { Retention = TimeSpan.FromDays(90) };
        Service = new AuditRecordPruneService(
            MockStoreManagerFactory.Object,
            Options.Create(options),
            MockTimeProvider.Object,
            NullLogger<AuditRecordPruneService>.Instance
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

    private void SetupClock()
    {
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
    }

    private void SetupStores()
    {
        SetupClock();
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

    private void SetupRemoveBefore(int removed, Times times, CancellationToken cancellationToken)
    {
        MockAuditRecordStore
            .Setup(x =>
                x.RemoveBeforeAsync(
                    Now.AddDays(-90),
                    AuditRecordPruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(removed)
            .Verifiable(times);
    }

    private void SetupSaveChangesFailingOnce(
        Exception exception,
        CancellationToken cancellationToken
    )
    {
        var saves = 0;
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .Returns(() =>
                ++saves == 1 ? ValueTask.FromException(exception) : ValueTask.CompletedTask
            )
            .Verifiable(Times.Exactly(2));
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenDisabled_EndsWithoutPruning()
    {
        var options = new AuditRecordOptions { Enabled = false };
        using var service = new AuditRecordPruneService(
            MockStoreManagerFactory.Object,
            Options.Create(options),
            MockTimeProvider.Object,
            NullLogger<AuditRecordPruneService>.Instance
        );

        await service.StartAsync(CancellationToken.None);

        Assert.NotNull(service.ExecuteTask);
        await service.ExecuteTask.WaitAsync(TimeSpan.FromSeconds(5));
    }

    #endregion

    #region PruneAsync Tests

    [Fact]
    public async Task PruneAsync_RemovesTheRecordsPastRetention()
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
        SetupSaveChangesFailingOnce(
            new InvalidOperationException("unreachable"),
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
        SetupSaveChangesFailingOnce(new TaskCanceledException("timed out"), cancellationToken);

        await Service.PruneAsync(cancellationToken);
        await Service.PruneAsync(cancellationToken);
    }

    [Fact]
    public async Task PruneAsync_WhenTheTokenIsCancelled_Throws()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = cancellationTokenSource.Token;
        await cancellationTokenSource.CancelAsync();
        SetupClock();
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
        MockAuditRecordStore
            .SetupSequence(x =>
                x.RemoveBeforeAsync(
                    Now.AddDays(-90),
                    AuditRecordPruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(AuditRecordPruneService.BatchSize)
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
        SetupRemoveBefore(3, Times.Exactly(2), cancellationToken);
        SetupSaveChangesFailingOnce(new StoreConcurrencyException("conflict"), cancellationToken);

        await Service.PruneAsync(cancellationToken);
    }

    #endregion
}
