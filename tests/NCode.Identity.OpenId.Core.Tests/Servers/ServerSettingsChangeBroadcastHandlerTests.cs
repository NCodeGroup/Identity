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
