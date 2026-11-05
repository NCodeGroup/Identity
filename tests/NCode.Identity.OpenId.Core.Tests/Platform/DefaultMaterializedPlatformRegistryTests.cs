// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Platform;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Platform;

public class DefaultMaterializedPlatformRegistryTests : BaseTests
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(5.0);

    private DefaultMaterializedPlatformRegistry Registry { get; } = new();

    private Mock<OpenIdPlatform> CreatePlatform() => CreateStrictMock<OpenIdPlatform>();

    #region GetPlatformAsync Tests

    [Fact]
    public async Task GetPlatformAsync_WhenAPlatformIsRegisteredLater_ReturnsIt()
    {
        var mockPlatform = CreatePlatform();
        var pending = Registry.GetPlatformAsync(CancellationToken.None);
        Assert.False(pending.IsCompleted);

        Registry.Register(mockPlatform.Object);

        var actual = await pending.AsTask().WaitAsync(Deadline);
        Assert.Same(mockPlatform.Object, actual);
    }

    #endregion

    #region Register Tests

    [Fact]
    public async Task Register_WhenAPlatformIsAlreadyRegistered_KeepsTheFirst()
    {
        var mockFirst = CreatePlatform();
        var mockSecond = CreatePlatform();
        Registry.Register(mockFirst.Object);

        Registry.Register(mockSecond.Object);

        var actual = await Registry.GetPlatformAsync(CancellationToken.None);
        Assert.Same(mockFirst.Object, actual);
    }

    #endregion

    #region RefreshSettingsAsync Tests

    [Fact]
    public async Task RefreshSettingsAsync_WhenNoPlatformIsRegistered_DoesNothing()
    {
        await Registry.RefreshSettingsAsync(CancellationToken.None);
    }

    [Fact]
    public async Task RefreshSettingsAsync_RefreshesTheRegisteredPlatform()
    {
        using var cts = new CancellationTokenSource();
        var cancellationToken = cts.Token;
        var mockPlatform = CreatePlatform();
        mockPlatform
            .Setup(x => x.RefreshSettingsAsync(cancellationToken))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        Registry.Register(mockPlatform.Object);

        await Registry.RefreshSettingsAsync(cancellationToken);
    }

    #endregion
}
