// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Servers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Servers;

public class ServerSettingsChangeBroadcastHandlerTests : BaseTests
{
    private const string ServerId = "server-1";

    private Mock<IMaterializedServerRegistry> MockRegistry { get; }
    private ServerSettingsChangeBroadcastHandler Handler { get; }

    public ServerSettingsChangeBroadcastHandlerTests()
    {
        MockRegistry = CreateStrictMock<IMaterializedServerRegistry>();
        Handler = new ServerSettingsChangeBroadcastHandler(MockRegistry.Object);
    }

    #region Channel Tests

    [Fact]
    public void Channel_IsTheServerSettingsChangedChannel()
    {
        Assert.Equal("server-settings-changed", Handler.Channel);
        Assert.Equal(ServerSettingsChangeBroadcastHandler.ChannelName, Handler.Channel);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_RefreshesTheAnnouncedServersSettings()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        MockRegistry
            .Setup(x => x.RefreshSettingsAsync(ServerId, cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(ServerId, cancellationToken);
    }

    #endregion

    #region HandleMissedMessagesAsync Tests

    [Fact]
    public async Task HandleMissedMessagesAsync_RefreshesTheServersSettings()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        MockRegistry
            .Setup(x => x.RefreshSettingsAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleMissedMessagesAsync(cancellationToken);
    }

    #endregion
}
