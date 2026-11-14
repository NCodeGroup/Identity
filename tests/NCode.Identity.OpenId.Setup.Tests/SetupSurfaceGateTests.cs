// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.AspNetCore.Options;

namespace NCode.Identity.OpenId.Setup;

public sealed class SetupSurfaceGateTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IInstanceSetupService> MockInstanceSetupService { get; }

    public SetupSurfaceGateTests()
    {
        MockInstanceSetupService = MockRepository.Create<IInstanceSetupService>();
    }

    public void Dispose() => MockRepository.Verify();

    #region Helpers

    private DefaultSetupSurfaceGate CreateGate(bool localDevelopmentEnabled = false) =>
        new(
            MockInstanceSetupService.Object,
            Options.Create(new LocalDevelopmentOptions { Enabled = localDevelopmentEnabled })
        );

    private void SetupIsInitialized(bool isInitialized) =>
        MockInstanceSetupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(isInitialized)
            .Verifiable(Times.Once());

    private static DefaultHttpContext CreateHttpContext(
        bool isHttps,
        IPAddress? remoteIpAddress = null
    )
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = isHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        httpContext.Connection.RemoteIpAddress = remoteIpAddress;
        return httpContext;
    }

    #endregion

    #region IsServedAsync Tests

    [Fact]
    public async Task IsServedAsync_WhenTheInstanceIsNotInitialized_IsServed()
    {
        SetupIsInitialized(false);
        var gate = CreateGate();

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: true),
            CancellationToken.None
        );

        Assert.True(isServed);
    }

    [Fact]
    public async Task IsServedAsync_WhenTheInstanceIsInitialized_IsNotServed()
    {
        SetupIsInitialized(true);
        var gate = CreateGate();

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: true),
            CancellationToken.None
        );

        Assert.False(isServed);
    }

    [Fact]
    public async Task IsServedAsync_WhenTheSwitchIsOffAndTheRequestIsNotHttps_IsNotServedWithoutReadingTheInstance()
    {
        var gate = CreateGate(localDevelopmentEnabled: false);

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: false, IPAddress.Loopback),
            CancellationToken.None
        );

        Assert.False(isServed);
    }

    [Fact]
    public async Task IsServedAsync_WhenTheSwitchIsOffAndTheRequestIsHttps_IsServed()
    {
        SetupIsInitialized(false);
        var gate = CreateGate(localDevelopmentEnabled: false);

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: true),
            CancellationToken.None
        );

        Assert.True(isServed);
    }

    [Fact]
    public async Task IsServedAsync_WhenTheSwitchIsOnAndTheRequestIsHttpsFromANonLoopbackAddress_IsServed()
    {
        SetupIsInitialized(false);
        var gate = CreateGate(localDevelopmentEnabled: true);

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: true, IPAddress.Parse("203.0.113.7")),
            CancellationToken.None
        );

        Assert.True(isServed);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task IsServedAsync_WhenTheSwitchIsOnAndTheRequestIsHttpFromALoopbackAddress_IsServed(
        string remoteIpAddress
    )
    {
        SetupIsInitialized(false);
        var gate = CreateGate(localDevelopmentEnabled: true);

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: false, IPAddress.Parse(remoteIpAddress)),
            CancellationToken.None
        );

        Assert.True(isServed);
    }

    [Fact]
    public async Task IsServedAsync_WhenTheSwitchIsOnAndTheRequestIsHttpFromANonLoopbackAddress_IsNotServedWithoutReadingTheInstance()
    {
        var gate = CreateGate(localDevelopmentEnabled: true);

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: false, IPAddress.Parse("203.0.113.7")),
            CancellationToken.None
        );

        Assert.False(isServed);
    }

    [Fact]
    public async Task IsServedAsync_WhenTheSwitchIsOnAndTheRequestIsHttpWithoutARemoteAddress_IsNotServedWithoutReadingTheInstance()
    {
        var gate = CreateGate(localDevelopmentEnabled: true);

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: false, remoteIpAddress: null),
            CancellationToken.None
        );

        Assert.False(isServed);
    }

    [Fact]
    public async Task IsServedAsync_WhenTheSwitchIsOnAndTheInstanceIsInitialized_IsNotServed()
    {
        SetupIsInitialized(true);
        var gate = CreateGate(localDevelopmentEnabled: true);

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: false, IPAddress.Loopback),
            CancellationToken.None
        );

        Assert.False(isServed);
    }

    #endregion
}
