// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Platform;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Platform;

public class DefaultMaterializedServerRegistryTests : BaseTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5.0);

    private DefaultMaterializedPlatformRegistry Registry { get; } = new();

    private Mock<OpenIdPlatform> CreateServer() => CreateStrictMock<OpenIdPlatform>();

    #region GetPlatformAsync Tests

    [Fact]
    public async Task GetServerAsync_WhenAServerIsRegisteredLater_ReturnsIt()
    {
        var mockServer = CreateServer();
        var pending = Registry.GetPlatformAsync(CancellationToken.None);
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

        var actual = await Registry.GetPlatformAsync(CancellationToken.None);
        Assert.Same(mockFirst.Object, actual);
    }

    #endregion

    #region RefreshSettingsAsync Tests

    [Fact]
    public async Task RefreshSettingsAsync_WhenNoServerIsRegistered_DoesNothing()
    {
        await Registry.RefreshSettingsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RefreshSettingsAsync_RefreshesTheRegisteredServer()
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
