// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
    private ServerSettingsChangedHandler Handler { get; }

    public ServerSettingsChangedHandlerTests()
    {
        MockRegistry = CreateStrictMock<IMaterializedServerRegistry>();
        MockReplicaBroadcaster = CreateStrictMock<IReplicaBroadcaster>();
        Handler = new ServerSettingsChangedHandler(
            MockRegistry.Object,
            MockReplicaBroadcaster.Object,
            CreateLooseMock<ILogger<ServerSettingsChangedHandler>>().Object
        );
    }

    private static ServerSettingsChangedCommand CreateCommand() => new(ServerId, default, default);

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_RefreshesTheServersSettingsAndAnnouncesTheChange()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var calls = new List<string>();
        MockRegistry
            .Setup(x => x.RefreshSettingsAsync(ServerId, cancellationToken))
            .Callback(() => calls.Add("refresh"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    ServerSettingsChangeBroadcastHandler.ChannelName,
                    ServerId,
                    cancellationToken
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
            .Returns(ValueTask.FromException(new InvalidOperationException("The refresh failed.")))
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
            .Returns(
                ValueTask.FromException(new InvalidOperationException("The broadcast failed."))
            )
            .Verifiable();

        await Handler.HandleAsync(CreateCommand(), CancellationToken.None);
    }

    #endregion
}
