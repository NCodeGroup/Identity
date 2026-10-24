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
using Moq;
using NCode.Identity.SharedState.InProcess;
using Xunit;

namespace NCode.Identity.SharedState.Tests.InProcess;

public sealed class DefaultInProcessReplicaBroadcasterTests : BaseTests
{
    private static DefaultInProcessReplicaBroadcaster CreateBroadcaster(
        params IReplicaBroadcastHandler[] handlers
    ) => new(handlers, NullLogger<DefaultInProcessReplicaBroadcaster>.Instance);

    #region PublishAsync Tests

    [Fact]
    public async Task PublishAsync_DeliversTheMessageToEachHandlerOfTheChannel()
    {
        var mockFirst = CreateStrictMock<IReplicaBroadcastHandler>();
        mockFirst.Setup(x => x.Channel).Returns("tenant-changed").Verifiable();
        mockFirst
            .Setup(x => x.HandleAsync("acme", CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var mockSecond = CreateStrictMock<IReplicaBroadcastHandler>();
        mockSecond.Setup(x => x.Channel).Returns("tenant-changed").Verifiable();
        mockSecond
            .Setup(x => x.HandleAsync("acme", CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var broadcaster = CreateBroadcaster(mockFirst.Object, mockSecond.Object);

        await broadcaster.PublishAsync("tenant-changed", "acme", CancellationToken.None);

        mockFirst.Verify(x => x.HandleAsync("acme", CancellationToken.None), Times.Once);
        mockSecond.Verify(x => x.HandleAsync("acme", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_SkipsTheHandlersOfOtherChannels()
    {
        var mockMatching = CreateStrictMock<IReplicaBroadcastHandler>();
        mockMatching.Setup(x => x.Channel).Returns("tenant-changed").Verifiable();
        mockMatching
            .Setup(x => x.HandleAsync("acme", CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var mockOther = CreateStrictMock<IReplicaBroadcastHandler>();
        mockOther.Setup(x => x.Channel).Returns("Tenant-Changed").Verifiable();

        var broadcaster = CreateBroadcaster(mockMatching.Object, mockOther.Object);

        await broadcaster.PublishAsync("tenant-changed", "acme", CancellationToken.None);

        mockMatching.Verify(x => x.HandleAsync("acme", CancellationToken.None), Times.Once);
    }

    [Fact]
    public async Task PublishAsync_WhenAHandlerFails_DeliversToTheOthersAndCompletes()
    {
        var mockFailing = CreateStrictMock<IReplicaBroadcastHandler>();
        mockFailing.Setup(x => x.Channel).Returns("tenant-changed").Verifiable();
        mockFailing
            .Setup(x => x.HandleAsync("acme", CancellationToken.None))
            .Throws(new InvalidOperationException("The handler failed."))
            .Verifiable();

        var mockSucceeding = CreateStrictMock<IReplicaBroadcastHandler>();
        mockSucceeding.Setup(x => x.Channel).Returns("tenant-changed").Verifiable();
        mockSucceeding
            .Setup(x => x.HandleAsync("acme", CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var broadcaster = CreateBroadcaster(mockFailing.Object, mockSucceeding.Object);

        await broadcaster.PublishAsync("tenant-changed", "acme", CancellationToken.None);

        mockSucceeding.Verify(x => x.HandleAsync("acme", CancellationToken.None), Times.Once);
    }

    #endregion
}
