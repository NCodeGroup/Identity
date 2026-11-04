// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Platform;

public sealed class ServerSettingsReconciliationServiceTests : BaseTests
{
    private const string PlatformId = "default";

    private static TimeSpan Deadline { get; } = TimeSpan.FromSeconds(5);

    private OpenIdOptions OpenIdOptions { get; } =
        new() { Platform = { ReconciliationLeaseDuration = TimeSpan.FromMilliseconds(20) } };

    private Mock<IMaterializedPlatformRegistry> MockRegistry { get; }
    private Mock<IOpenIdPlatformProvider> MockServerProvider { get; }
    private Mock<IOpenIdEnvironmentProvider> MockEnvironmentProvider { get; }
    private Mock<OpenIdEnvironment> MockEnvironment { get; }
    private Mock<IPlatformSettingsReconciler> MockReconciler { get; }
    private Mock<OpenIdPlatform> MockServer { get; }
    private Mock<IReadOnlySettingCollectionProvider> MockSettingsProvider { get; }
    private PlatformSettingsReconciliationService Service { get; }

    // The source of the change token that the settings provider hands out next.
    private CancellationTokenSource Changes { get; set; } = new();

    // One completion source per reconciler call, so a test awaits the call it needs.
    private TaskCompletionSource[] Calls { get; } =
    [
        new(TaskCreationOptions.RunContinuationsAsynchronously),
        new(TaskCreationOptions.RunContinuationsAsynchronously),
        new(TaskCreationOptions.RunContinuationsAsynchronously),
    ];

    private int _callCount;

    public ServerSettingsReconciliationServiceTests()
    {
        MockRegistry = CreateStrictMock<IMaterializedPlatformRegistry>();
        MockServerProvider = CreateStrictMock<IOpenIdPlatformProvider>();
        MockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        MockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        MockReconciler = CreateStrictMock<IPlatformSettingsReconciler>();
        MockServer = CreateStrictMock<OpenIdPlatform>();
        MockSettingsProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();

        Service = new PlatformSettingsReconciliationService(
            MockRegistry.Object,
            MockServerProvider.Object,
            MockEnvironmentProvider.Object,
            MockReconciler.Object,
            Options.Create(OpenIdOptions),
            TimeProvider.System,
            CreateLooseMock<ILogger<PlatformSettingsReconciliationService>>().Object
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Service.Dispose();
            Changes.Dispose();
        }

        base.Dispose(disposing);
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_WhenTheServerIsMaterialized_ReconcilesItsSettings()
    {
        SetupServer();
        SetupReconciler(_ => ValueTask.FromResult(true));

        await Service.StartAsync(CancellationToken.None);
        await Calls[0].Task.WaitAsync(Deadline);
        await Service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMaterializingAtStartFails_ReconcilesOnceARequestMaterializesTheServer()
    {
        SetupServerMembers();
        MockEnvironmentProvider.Setup(x => x.Get()).Returns(MockEnvironment.Object).Verifiable();
        MockServerProvider
            .Setup(x => x.GetAsync(MockEnvironment.Object, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unreachable"))
            .Verifiable();
        MockRegistry
            .Setup(x => x.GetPlatformAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockServer.Object)
            .Verifiable();
        SetupReconciler(_ => ValueTask.FromResult(true));

        await Service.StartAsync(CancellationToken.None);
        await Calls[0].Task.WaitAsync(Deadline);
        await Service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheServerSettingsChange_ReconcilesThemAgain()
    {
        SetupServer();
        SetupReconciler(_ => ValueTask.FromResult(true));

        await Service.StartAsync(CancellationToken.None);
        await Calls[0].Task.WaitAsync(Deadline);
        SignalChange();
        await Calls[1].Task.WaitAsync(Deadline);
        await Service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheReconciliationIsUnsettled_TriesAgainAfterTheLease()
    {
        SetupServer();
        SetupReconciler(call => ValueTask.FromResult(call > 1));

        await Service.StartAsync(CancellationToken.None);
        await Calls[1].Task.WaitAsync(Deadline);
        await Service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheReconciliationFails_TriesAgainAfterTheLease()
    {
        SetupServer();
        SetupReconciler(call =>
            call > 1
                ? ValueTask.FromResult(true)
                : ValueTask.FromException<bool>(new InvalidOperationException("boom"))
        );

        await Service.StartAsync(CancellationToken.None);
        await Calls[1].Task.WaitAsync(Deadline);
        await Service.StopAsync(CancellationToken.None);
    }

    #endregion

    #region StopAsync Tests

    [Fact]
    public async Task StopAsync_BeforeTheServerIsMaterialized_Completes()
    {
        // The host may run ExecuteAsync on another thread, so the stop waits until the service is waiting for the server.
        var requested = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        MockEnvironmentProvider.Setup(x => x.Get()).Returns(MockEnvironment.Object).Verifiable();
        MockServerProvider
            .Setup(x => x.GetAsync(MockEnvironment.Object, It.IsAny<CancellationToken>()))
            .Returns(
                (OpenIdEnvironment _, CancellationToken cancellationToken) =>
                {
                    requested.TrySetResult();
                    return WaitForeverAsync(cancellationToken);
                }
            )
            .Verifiable();

        await Service.StartAsync(CancellationToken.None);
        await requested.Task.WaitAsync(Deadline);
        await Service.StopAsync(CancellationToken.None).WaitAsync(Deadline);

        Assert.NotNull(Service.ExecuteTask);
        Assert.True(Service.ExecuteTask.IsCompletedSuccessfully);
    }

    #endregion

    #region Scaffolding

    private static async ValueTask<OpenIdPlatform> WaitForeverAsync(
        CancellationToken cancellationToken
    )
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        throw new InvalidOperationException("The wait for the server ended without a server.");
    }

    // Replaces the source before it cancels the old one, so the producer never hands out a cancelled token again.
    private void SignalChange()
    {
        var previous = Changes;
        Changes = new CancellationTokenSource();
        previous.Cancel();
        previous.Dispose();
    }

    private void SetupServer()
    {
        SetupServerMembers();
        MockEnvironmentProvider.Setup(x => x.Get()).Returns(MockEnvironment.Object).Verifiable();
        MockServerProvider
            .Setup(x => x.GetAsync(MockEnvironment.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockServer.Object)
            .Verifiable();
    }

    private void SetupServerMembers()
    {
        MockSettingsProvider
            .Setup(x => x.GetChangeToken())
            .Returns(() => new CancellationChangeToken(Changes.Token))
            .Verifiable();
        MockServer.Setup(x => x.PlatformId).Returns(PlatformId).Verifiable();
        MockServer.Setup(x => x.SettingsProvider).Returns(MockSettingsProvider.Object).Verifiable();
    }

    // Counts the calls and completes the call's completion source after choosing its result.
    private void SetupReconciler(Func<int, ValueTask<bool>> result) =>
        MockReconciler
            .Setup(x => x.ReconcileAsync(PlatformId, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                var call = Interlocked.Increment(ref _callCount);
                var outcome = result(call);
                if (call <= Calls.Length)
                {
                    Calls[call - 1].TrySetResult();
                }

                return outcome;
            })
            .Verifiable();

    #endregion
}
