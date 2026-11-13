// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Setup;

public sealed class InstanceSetupHostedServiceTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IInstanceSetupService> MockInstanceSetupService { get; }
    private Mock<ISetupTokenService> MockSetupTokenService { get; }
    private List<string> CallLog { get; } = [];

    public InstanceSetupHostedServiceTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockInstanceSetupService = MockRepository.Create<IInstanceSetupService>();
        MockSetupTokenService = MockRepository.Create<ISetupTokenService>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private InstanceSetupHostedService CreateService() =>
        new(MockInstanceSetupService.Object, MockSetupTokenService.Object);

    private void SetupIsInitialized(bool isInitialized)
    {
        MockInstanceSetupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(isInitialized)
            .Verifiable(Times.Once());
    }

    private void SetupEnsureToken()
    {
        MockSetupTokenService
            .Setup(x => x.EnsureTokenAsync(It.IsAny<CancellationToken>()))
            .Callback(() => CallLog.Add("token"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once());
    }

    #endregion

    #region StartAsync Tests

    [Fact]
    public async Task StartAsync_WhenInitialized_DoesNotEnsureToken()
    {
        SetupIsInitialized(true);

        await CreateService().StartAsync(CancellationToken.None);

        Assert.Empty(CallLog);
    }

    [Fact]
    public async Task StartAsync_WhenUninitialized_EnsuresTokenOnce()
    {
        SetupIsInitialized(false);
        SetupEnsureToken();

        await CreateService().StartAsync(CancellationToken.None);

        Assert.Equal(["token"], CallLog);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_Always_Completes()
    {
        await CreateService().StopAsync(CancellationToken.None);

        Assert.Empty(CallLog);
    }

    #endregion
}
