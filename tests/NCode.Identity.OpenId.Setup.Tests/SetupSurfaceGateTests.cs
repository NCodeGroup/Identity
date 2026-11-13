// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using HostEnvironments = Microsoft.Extensions.Hosting.Environments;

namespace NCode.Identity.OpenId.Setup;

public sealed class SetupSurfaceGateTests : IDisposable
{
    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);
    private Mock<IInstanceSetupService> MockInstanceSetupService { get; }
    private Mock<IHostEnvironment> MockHostEnvironment { get; }

    public SetupSurfaceGateTests()
    {
        MockInstanceSetupService = MockRepository.Create<IInstanceSetupService>();
        MockHostEnvironment = MockRepository.Create<IHostEnvironment>();
    }

    public void Dispose() => MockRepository.Verify();

    #region Helpers

    private DefaultSetupSurfaceGate CreateGate(string environmentName)
    {
        MockHostEnvironment.Setup(x => x.EnvironmentName).Returns(environmentName).Verifiable();

        return new DefaultSetupSurfaceGate(
            MockInstanceSetupService.Object,
            MockHostEnvironment.Object
        );
    }

    private void SetupIsInitialized(bool isInitialized) =>
        MockInstanceSetupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(isInitialized)
            .Verifiable(Times.Once());

    private static DefaultHttpContext CreateHttpContext(bool isHttps)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = isHttps ? Uri.UriSchemeHttps : Uri.UriSchemeHttp;
        return httpContext;
    }

    #endregion

    #region IsServedAsync Tests

    [Fact]
    public async Task IsServedAsync_WhenTheInstanceIsNotInitialized_IsServed()
    {
        SetupIsInitialized(false);
        var gate = CreateGate(HostEnvironments.Development);

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
        var gate = CreateGate(HostEnvironments.Development);

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: true),
            CancellationToken.None
        );

        Assert.False(isServed);
    }

    [Fact]
    public async Task IsServedAsync_WhenProductionAndTheRequestIsNotHttps_IsNotServedWithoutReadingTheInstance()
    {
        var gate = CreateGate(HostEnvironments.Production);

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: false),
            CancellationToken.None
        );

        Assert.False(isServed);
    }

    [Fact]
    public async Task IsServedAsync_WhenProductionAndTheRequestIsHttps_IsServed()
    {
        SetupIsInitialized(false);
        var gate = CreateGate(HostEnvironments.Production);

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: true),
            CancellationToken.None
        );

        Assert.True(isServed);
    }

    [Fact]
    public async Task IsServedAsync_WhenProductionAndTheInstanceIsInitialized_IsNotServed()
    {
        SetupIsInitialized(true);
        var gate = CreateGate(HostEnvironments.Production);

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: true),
            CancellationToken.None
        );

        Assert.False(isServed);
    }

    [Fact]
    public async Task IsServedAsync_WhenDevelopmentAndTheRequestIsNotHttps_IsServed()
    {
        SetupIsInitialized(false);
        var gate = CreateGate(HostEnvironments.Development);

        var isServed = await gate.IsServedAsync(
            CreateHttpContext(isHttps: false),
            CancellationToken.None
        );

        Assert.True(isServed);
    }

    #endregion
}
