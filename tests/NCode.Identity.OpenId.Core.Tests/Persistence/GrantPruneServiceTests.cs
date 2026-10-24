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
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Persistence;

public sealed class GrantPruneServiceTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private MockRepository MockRepository { get; }
    private Mock<TimeProvider> MockTimeProvider { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<IGrantStore> MockGrantStore { get; }
    private GrantPruneService Service { get; }

    public GrantPruneServiceTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockTimeProvider = MockRepository.Create<TimeProvider>();
        MockStoreManagerFactory = MockRepository.Create<IStoreManagerFactory>();
        MockStoreManager = MockRepository.Create<IStoreManager>();
        MockGrantStore = MockRepository.Create<IGrantStore>();
        Service = new GrantPruneService(
            MockStoreManagerFactory.Object,
            Options.Create(new GrantRetentionOptions()),
            MockTimeProvider.Object,
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
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
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
        MockGrantStore
            .SetupSequence(x =>
                x.RemoveEndedBeforeAsync(
                    Now - TimeSpan.FromDays(14),
                    GrantPruneService.GrantTypesEndedByRevocation,
                    GrantPruneService.BatchSize,
                    cancellationToken
                )
            )
            .ReturnsAsync(GrantPruneService.BatchSize)
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
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new StoreConcurrencyException("conflict"))
            .Returns(ValueTask.CompletedTask);

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
        MockStoreManager
            .SetupSequence(x => x.SaveChangesAsync(cancellationToken))
            .ThrowsAsync(new InvalidOperationException("unreachable"))
            .Returns(ValueTask.CompletedTask);

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
