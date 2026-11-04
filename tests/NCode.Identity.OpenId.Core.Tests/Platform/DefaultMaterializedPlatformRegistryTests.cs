// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Platform;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Platform;

public class DefaultMaterializedServerRegistryTests : BaseTests
{
    private const string PlatformId = "server-1";

    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5.0);

    private DefaultMaterializedPlatformRegistry Registry { get; } = new();

    private Mock<OpenIdPlatform> CreateServer(string? platformId = null)
    {
        var mockServer = CreateStrictMock<OpenIdPlatform>();
        if (platformId is not null)
            mockServer.Setup(x => x.PlatformId).Returns(platformId).Verifiable();

        return mockServer;
    }

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
    public async Task RefreshSettingsAsync_WhenTheServerIdMatches_RefreshesTheServer()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var mockServer = CreateServer(PlatformId);
        mockServer
            .Setup(x => x.RefreshSettingsAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        Registry.Register(mockServer.Object);

        await Registry.RefreshSettingsAsync(PlatformId, cancellationToken);
    }

    [Fact]
    public async Task RefreshSettingsAsync_WhenTheServerIdDiffers_DoesNothing()
    {
        var mockServer = CreateServer(PlatformId);
        Registry.Register(mockServer.Object);

        await Registry.RefreshSettingsAsync("another-server", CancellationToken.None);
    }

    [Fact]
    public async Task RefreshSettingsAsync_WhenNoServerIsRegistered_DoesNothing()
    {
        await Registry.RefreshSettingsAsync(PlatformId, CancellationToken.None);
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
