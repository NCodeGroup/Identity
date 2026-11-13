// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Tenants;

namespace NCode.Identity.OpenId.Setup;

public sealed class InstanceSetupHostedServiceTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IInstanceSetupService> MockInstanceSetupService { get; }
    private Mock<ISetupTokenService> MockSetupTokenService { get; }
    private Mock<ITenantResolver> MockTenantResolver { get; }
    private Mock<IServiceScopeFactory> MockServiceScopeFactory { get; }
    private Mock<IServiceScope> MockServiceScope { get; }
    private Mock<IServiceProvider> MockServiceProvider { get; }
    private List<string> CallLog { get; } = [];

    public InstanceSetupHostedServiceTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockInstanceSetupService = MockRepository.Create<IInstanceSetupService>();
        MockSetupTokenService = MockRepository.Create<ISetupTokenService>();
        MockTenantResolver = MockRepository.Create<ITenantResolver>();
        MockServiceScopeFactory = MockRepository.Create<IServiceScopeFactory>();
        MockServiceScope = MockRepository.Create<IServiceScope>();
        MockServiceProvider = MockRepository.Create<IServiceProvider>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    #region Helpers

    private InstanceSetupHostedService CreateService() =>
        new(
            MockInstanceSetupService.Object,
            MockSetupTokenService.Object,
            MockTenantResolver.Object,
            MockServiceScopeFactory.Object
        );

    private void SetupIsInitialized(bool isInitialized)
    {
        MockInstanceSetupService
            .Setup(x => x.IsInitializedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(isInitialized)
            .Verifiable(Times.Once());
    }

    private void SetupResolveControlPlaneTenant()
    {
        MockServiceScopeFactory
            .Setup(x => x.CreateScope())
            .Returns(MockServiceScope.Object)
            .Verifiable(Times.Once());
        MockServiceScope
            .Setup(x => x.ServiceProvider)
            .Returns(MockServiceProvider.Object)
            .Verifiable(Times.Once());
        MockServiceScope.Setup(x => x.Dispose()).Verifiable(Times.Once());

        MockTenantResolver
            .Setup(x =>
                x.ResolveControlPlaneTenantAsync(
                    It.Is<HttpContext>(httpContext =>
                        ReferenceEquals(httpContext.RequestServices, MockServiceProvider.Object)
                    ),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => CallLog.Add("tenant"))
            .ReturnsAsync(new PersistedTenantBuilder().Build())
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
    public async Task StartAsync_WhenInitialized_DoesNotResolveTenantOrEnsureToken()
    {
        SetupIsInitialized(true);

        await CreateService().StartAsync(CancellationToken.None);

        Assert.Empty(CallLog);
    }

    [Fact]
    public async Task StartAsync_WhenUninitialized_ResolvesTenantThenEnsuresToken()
    {
        SetupIsInitialized(false);
        SetupResolveControlPlaneTenant();
        SetupEnsureToken();

        await CreateService().StartAsync(CancellationToken.None);

        Assert.Equal(["tenant", "token"], CallLog);
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
