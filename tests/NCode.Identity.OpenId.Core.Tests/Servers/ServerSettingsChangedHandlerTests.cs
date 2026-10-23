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
