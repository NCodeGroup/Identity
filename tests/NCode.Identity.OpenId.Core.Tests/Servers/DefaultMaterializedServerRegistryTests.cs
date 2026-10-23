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

public class DefaultMaterializedServerRegistryTests : BaseTests
{
    private const string ServerId = "server-1";

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5.0);

    private DefaultMaterializedServerRegistry Registry { get; } = new();

    private Mock<OpenIdServer> CreateServer(string? serverId = null)
    {
        var mockServer = CreateStrictMock<OpenIdServer>();
        if (serverId is not null)
            mockServer.Setup(x => x.ServerId).Returns(serverId).Verifiable();

        return mockServer;
    }

    #region GetServerAsync Tests

    [Fact]
    public async Task GetServerAsync_WhenAServerIsRegisteredLater_ReturnsIt()
    {
        var mockServer = CreateServer();
        var pending = Registry.GetServerAsync(CancellationToken.None);
        Assert.False(pending.IsCompleted);

        Registry.Register(mockServer.Object);

        var actual = await pending.AsTask().WaitAsync(Deadline);
        Assert.Same(mockServer.Object, actual);
    }

    #endregion

    #region Register Tests

    [Fact]
    public async Task Register_WhenAServerIsAlreadyRegistered_KeepsTheFirst()
    {
        var mockFirst = CreateServer();
        var mockSecond = CreateServer();
        Registry.Register(mockFirst.Object);

        Registry.Register(mockSecond.Object);

        var actual = await Registry.GetServerAsync(CancellationToken.None);
        Assert.Same(mockFirst.Object, actual);
    }

    #endregion

    #region RefreshSettingsAsync Tests

    [Fact]
    public async Task RefreshSettingsAsync_WhenTheServerIdMatches_RefreshesTheServer()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var mockServer = CreateServer(ServerId);
        mockServer
            .Setup(x => x.RefreshSettingsAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        Registry.Register(mockServer.Object);

        await Registry.RefreshSettingsAsync(ServerId, cancellationToken);
    }

    [Fact]
    public async Task RefreshSettingsAsync_WhenTheServerIdDiffers_DoesNothing()
    {
        var mockServer = CreateServer(ServerId);
        Registry.Register(mockServer.Object);

        await Registry.RefreshSettingsAsync("another-server", CancellationToken.None);
    }

    [Fact]
    public async Task RefreshSettingsAsync_WhenNoServerIsRegistered_DoesNothing()
    {
        await Registry.RefreshSettingsAsync(ServerId, CancellationToken.None);
        await Registry.RefreshSettingsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RefreshSettingsAsync_WithoutAServerId_RefreshesTheRegisteredServer()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var mockServer = CreateServer();
        mockServer
            .Setup(x => x.RefreshSettingsAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        Registry.Register(mockServer.Object);

        await Registry.RefreshSettingsAsync(cancellationToken);
    }

    #endregion
}
