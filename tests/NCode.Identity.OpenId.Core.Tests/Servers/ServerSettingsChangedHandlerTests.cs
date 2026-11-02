// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Linq.Expressions;
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Servers;

public class ServerSettingsChangedHandlerTests : BaseTests
{
    private const string ServerId = "server-1";

    private Mock<IMaterializedServerRegistry> MockRegistry { get; }
    private Mock<IReplicaBroadcaster> MockReplicaBroadcaster { get; }
    private Mock<TimeProvider> MockTimeProvider { get; }
    private ServerSettingsChangedHandler Handler { get; }

    public ServerSettingsChangedHandlerTests()
    {
        MockRegistry = CreateStrictMock<IMaterializedServerRegistry>();
        MockReplicaBroadcaster = CreateStrictMock<IReplicaBroadcaster>();
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockTimeProvider
            .Setup(CreateTimerExpression)
            .Returns(
                (TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
                    TimeProvider.System.CreateTimer(callback, state, dueTime, period)
            )
            .Verifiable();
        Handler = new ServerSettingsChangedHandler(
            MockRegistry.Object,
            MockReplicaBroadcaster.Object,
            MockTimeProvider.Object,
            CreateLooseMock<ILogger<ServerSettingsChangedHandler>>().Object
        );
    }

    private static Expression<Func<TimeProvider, ITimer>> CreateTimerExpression =>
        x =>
            x.CreateTimer(
                It.IsAny<TimerCallback>(),
                It.IsAny<object?>(),
                PostCommitWork.Timeout,
                Timeout.InfiniteTimeSpan
            );

    private static ServerSettingsChangedCommand CreateCommand() => new(ServerId, default, default);

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_RefreshesTheServersSettingsAndAnnouncesTheChange()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var calls = new List<string>();
        MockRegistry
            .Setup(x => x.RefreshSettingsAsync(ServerId, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("refresh"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    ServerSettingsChangeBroadcastHandler.ChannelName,
                    ServerId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => calls.Add("announce"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(CreateCommand(), cancellationToken);

        Assert.Equal(["refresh", "announce"], calls);
    }

    [Fact]
    public async Task HandleAsync_WhenTheRefreshFails_StillAnnouncesTheChange()
    {
        MockRegistry
            .Setup(x => x.RefreshSettingsAsync(ServerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The refresh failed."))
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    ServerSettingsChangeBroadcastHandler.ChannelName,
                    ServerId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTheAnnouncementFails_DoesNotThrow()
    {
        MockRegistry
            .Setup(x => x.RefreshSettingsAsync(ServerId, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    ServerSettingsChangeBroadcastHandler.ChannelName,
                    ServerId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("The broadcast failed."))
            .Verifiable();

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTheRefreshTimesOut_StillAnnouncesTheChange()
    {
        MockRegistry
            .Setup(x => x.RefreshSettingsAsync(ServerId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TaskCanceledException("The refresh timed out."))
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    ServerSettingsChangeBroadcastHandler.ChannelName,
                    ServerId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTheAnnouncementTimesOut_DoesNotThrow()
    {
        MockRegistry
            .Setup(x => x.RefreshSettingsAsync(ServerId, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    ServerSettingsChangeBroadcastHandler.ChannelName,
                    ServerId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new TaskCanceledException("The announcement timed out."))
            .Verifiable();

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerCancels_StillRefreshesAndAnnounces()
    {
        var refreshToken = CancellationToken.None;
        var announcementToken = CancellationToken.None;
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        MockRegistry
            .Setup(x => x.RefreshSettingsAsync(ServerId, It.IsAny<CancellationToken>()))
            .Callback((string _, CancellationToken token) => refreshToken = token)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    ServerSettingsChangeBroadcastHandler.ChannelName,
                    ServerId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback((string _, string _, CancellationToken token) => announcementToken = token)
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(CreateCommand(), cancellation.Token);

        Assert.True(refreshToken.CanBeCanceled);
        Assert.False(refreshToken.IsCancellationRequested);
        Assert.True(announcementToken.CanBeCanceled);
        Assert.False(announcementToken.IsCancellationRequested);
    }

    [Fact]
    public async Task HandleAsync_BoundsTheRefreshAndTheAnnouncementByThePostCommitTimeout()
    {
        MockRegistry
            .Setup(x => x.RefreshSettingsAsync(ServerId, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    ServerSettingsChangeBroadcastHandler.ChannelName,
                    ServerId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);

        MockTimeProvider.Verify(CreateTimerExpression, Times.Exactly(2));
    }

    #endregion
}
