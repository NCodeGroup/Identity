// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Platform;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Platform;

public class ServerSettingsChangeBroadcastHandlerTests : BaseTests
{
    private const string PlatformId = "server-1";

    private Mock<IMaterializedPlatformRegistry> MockRegistry { get; }
    private PlatformSettingsChangeBroadcastHandler Handler { get; }

    public ServerSettingsChangeBroadcastHandlerTests()
    {
        MockRegistry = CreateStrictMock<IMaterializedPlatformRegistry>();
        Handler = new PlatformSettingsChangeBroadcastHandler(MockRegistry.Object);
    }

    #region Channel Tests

    [Fact]
    public void Channel_IsTheServerSettingsChangedChannel()
    {
        Assert.Equal("platform-settings-changed", Handler.Channel);
        Assert.Equal(PlatformSettingsChangeBroadcastHandler.ChannelName, Handler.Channel);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_RefreshesTheAnnouncedServersSettings()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        MockRegistry
            .Setup(x => x.RefreshSettingsAsync(PlatformId, cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(PlatformId, cancellationToken);
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
