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
using NCode.Identity.OpenId.Authentication.Usage;
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

    #region FlushAsync Tests

    [Fact]
    public async Task FlushAsync_WhenNothingWasRecorded_DoesNotOpenTheStore()
    {
        await Service.FlushAsync(CancellationToken.None);
    }

    [Fact]
    public async Task FlushAsync_AddsTheRecordedUsageInOneUnitOfWork()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        MockTenantUsageStore
            .Setup(x =>
                x.AddAsync("tenant-1", Today, TenantUsageMeters.Requests, 2, cancellationToken)
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
    public async Task FlushAsync_WhenTheCommitFails_KeepsTheUsageForTheNextFlush()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        MockTenantUsageStore
            .Setup(x =>
                x.AddAsync("tenant-1", Today, TenantUsageMeters.Requests, 1, cancellationToken)
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockStoreManager
            .Setup(x => x.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new InvalidOperationException("conflict"))
            .Verifiable();

        await Service.FlushAsync(cancellationToken);

        var usage = Assert.Single(Recorder.Drain());
        Assert.Equal(new TenantUsageKey("tenant-1", Today, TenantUsageMeters.Requests), usage.Key);
        Assert.Equal(1, usage.Value);
    }

    #endregion

    #region PruneAsync Tests

    [Fact]
    public async Task PruneAsync_RemovesTheDaysPastRetentionOncePerDay()
    {
        var cancellationToken = CancellationToken.None;
        SetupStores();
        MockTenantUsageStore
            .Setup(x => x.RemoveBeforeAsync(Today.AddDays(-10), cancellationToken))
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
            .Setup(x => x.RemoveBeforeAsync(Today.AddDays(-10), cancellationToken))
            .ReturnsAsync(3)
            .Verifiable(Times.Exactly(2));
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new InvalidOperationException("unreachable"))
            .Returns(ValueTask.CompletedTask);

        await Service.PruneAsync(cancellationToken);
        await Service.PruneAsync(cancellationToken);
    }

    #endregion
}
