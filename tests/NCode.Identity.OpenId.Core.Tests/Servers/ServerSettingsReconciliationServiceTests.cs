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
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Servers;

public sealed class ServerSettingsReconciliationServiceTests : BaseTests
{
    private const string ServerId = "default";

    private static TimeSpan Deadline { get; } = TimeSpan.FromSeconds(5);

    private OpenIdOptions OpenIdOptions { get; } =
        new() { Server = { ReconciliationLeaseDuration = TimeSpan.FromMilliseconds(20) } };

    private Mock<IMaterializedServerRegistry> MockRegistry { get; }
    private Mock<IServerSettingsReconciler> MockReconciler { get; }
    private Mock<OpenIdServer> MockServer { get; }
    private Mock<IReadOnlySettingCollectionProvider> MockSettingsProvider { get; }
    private ServerSettingsReconciliationService Service { get; }

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
        MockRegistry = CreateStrictMock<IMaterializedServerRegistry>();
        MockReconciler = CreateStrictMock<IServerSettingsReconciler>();
        MockServer = CreateStrictMock<OpenIdServer>();
        MockSettingsProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();

        Service = new ServerSettingsReconciliationService(
            MockRegistry.Object,
            MockReconciler.Object,
            Options.Create(OpenIdOptions),
            TimeProvider.System,
            CreateLooseMock<ILogger<ServerSettingsReconciliationService>>().Object
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
        MockRegistry
            .Setup(x => x.GetServerAsync(It.IsAny<CancellationToken>()))
            .Returns(
                (CancellationToken cancellationToken) =>
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

    private static async ValueTask<OpenIdServer> WaitForeverAsync(
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
        MockSettingsProvider
            .Setup(x => x.GetChangeToken())
            .Returns(() => new CancellationChangeToken(Changes.Token))
            .Verifiable();
        MockServer.Setup(x => x.ServerId).Returns(ServerId).Verifiable();
        MockServer.Setup(x => x.SettingsProvider).Returns(MockSettingsProvider.Object).Verifiable();
        MockRegistry
            .Setup(x => x.GetServerAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockServer.Object)
            .Verifiable();
    }

    // Counts the calls and completes the call's completion source after choosing its result.
    private void SetupReconciler(Func<int, ValueTask<bool>> result) =>
        MockReconciler
            .Setup(x => x.ReconcileAsync(ServerId, It.IsAny<CancellationToken>()))
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
