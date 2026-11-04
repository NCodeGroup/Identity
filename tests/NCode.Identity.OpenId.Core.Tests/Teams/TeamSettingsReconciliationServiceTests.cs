// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.Settings;
using NCode.Persistence.Stores;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Teams;

public sealed class TeamSettingsReconciliationServiceTests : BaseTests
{
    private const string TeamId = "acme";
    private const string OtherTeamId = "globex";

    private static TimeSpan Deadline { get; } = TimeSpan.FromSeconds(5);

    private OpenIdOptions OpenIdOptions { get; } =
        new() { Team = { ReconciliationLeaseDuration = TimeSpan.FromMilliseconds(20) } };

    private Mock<IMaterializedPlatformRegistry> MockRegistry { get; }
    private Mock<IOpenIdPlatformProvider> MockPlatformProvider { get; }
    private Mock<IOpenIdEnvironmentProvider> MockEnvironmentProvider { get; }
    private Mock<OpenIdEnvironment> MockEnvironment { get; }
    private Mock<ITeamSettingsReconciler> MockReconciler { get; }
    private Mock<IStoreManagerFactory> MockStoreManagerFactory { get; }
    private Mock<IStoreManager> MockStoreManager { get; }
    private Mock<ITeamStore> MockTeamStore { get; }
    private Channel<string?> Triggers { get; } =
        DefaultTeamSettingsReconciliationTrigger.CreateChannel();
    private DefaultTeamSettingsReconciliationTrigger Trigger { get; }
    private Mock<OpenIdPlatform> MockPlatform { get; }
    private Mock<IReadOnlySettingCollectionProvider> MockSettingsProvider { get; }
    private TeamSettingsReconciliationService Service { get; }

    // The source of the change token that the settings provider hands out next.
    private CancellationTokenSource Changes { get; set; } = new();

    // One completion source per reconciler call, so a test awaits the call it needs.
    private TaskCompletionSource[] Calls { get; } =
    [
        new(TaskCreationOptions.RunContinuationsAsynchronously),
        new(TaskCreationOptions.RunContinuationsAsynchronously),
        new(TaskCreationOptions.RunContinuationsAsynchronously),
        new(TaskCreationOptions.RunContinuationsAsynchronously),
    ];

    // The teams the reconciler was called for, in call order.
    private List<string> ReconciledTeams { get; } = [];

    private int _callCount;

    public TeamSettingsReconciliationServiceTests()
    {
        MockRegistry = CreateStrictMock<IMaterializedPlatformRegistry>();
        MockPlatformProvider = CreateStrictMock<IOpenIdPlatformProvider>();
        MockEnvironmentProvider = CreateStrictMock<IOpenIdEnvironmentProvider>();
        MockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        MockReconciler = CreateStrictMock<ITeamSettingsReconciler>();
        MockPlatform = CreateStrictMock<OpenIdPlatform>();
        MockSettingsProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        MockStoreManagerFactory = CreateStrictMock<IStoreManagerFactory>();
        MockStoreManager = CreateStrictMock<IStoreManager>();
        MockTeamStore = CreateStrictMock<ITeamStore>();
        Trigger = new DefaultTeamSettingsReconciliationTrigger(Triggers);

        Service = new TeamSettingsReconciliationService(
            MockRegistry.Object,
            MockPlatformProvider.Object,
            MockEnvironmentProvider.Object,
            MockStoreManagerFactory.Object,
            MockReconciler.Object,
            Trigger,
            Triggers,
            Options.Create(OpenIdOptions),
            TimeProvider.System,
            CreateLooseMock<ILogger<TeamSettingsReconciliationService>>().Object
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
    public async Task ExecuteAsync_WhenThePlatformIsMaterialized_ReconcilesItsSettings()
    {
        SetupPlatform();
        SetupTeams(TeamId);
        SetupReconciler(_ => ValueTask.FromResult(true));

        await Service.StartAsync(CancellationToken.None);
        await Calls[0].Task.WaitAsync(Deadline);
        await Service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMaterializingAtStartFails_ReconcilesOnceARequestMaterializesThePlatform()
    {
        SetupPlatformMembers();
        SetupTeams(TeamId);
        MockEnvironmentProvider.Setup(x => x.Get()).Returns(MockEnvironment.Object).Verifiable();
        MockPlatformProvider
            .Setup(x => x.GetAsync(MockEnvironment.Object, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("unreachable"))
            .Verifiable();
        MockRegistry
            .Setup(x => x.GetPlatformAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockPlatform.Object)
            .Verifiable();
        SetupReconciler(_ => ValueTask.FromResult(true));

        await Service.StartAsync(CancellationToken.None);
        await Calls[0].Task.WaitAsync(Deadline);
        await Service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenThePlatformSettingsChange_ReconcilesEveryTeam()
    {
        SetupPlatform();
        SetupTeams(TeamId, OtherTeamId);
        SetupReconciler(_ => ValueTask.FromResult(true));

        await Service.StartAsync(CancellationToken.None);
        await Calls[1].Task.WaitAsync(Deadline);
        SignalChange();
        await Calls[3].Task.WaitAsync(Deadline);
        await Service.StopAsync(CancellationToken.None);

        Assert.Equal([TeamId, OtherTeamId, TeamId, OtherTeamId], ReconciledTeams.Take(4));
    }

    [Fact]
    public async Task ExecuteAsync_WhenATeamIsTriggered_ReconcilesOnlyThatTeam()
    {
        // The pass over every team finds no team, so the only team reconciled is the one that is triggered.
        SetupPlatform();
        SetupTeams();
        SetupReconciler(_ => ValueTask.FromResult(true));

        await Service.StartAsync(CancellationToken.None);
        Trigger.Trigger(OtherTeamId);
        await Calls[0].Task.WaitAsync(Deadline);
        await Service.StopAsync(CancellationToken.None);

        Assert.Equal([OtherTeamId], ReconciledTeams);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheReconciliationIsUnsettled_TriesAgainAfterTheLease()
    {
        SetupPlatform();
        SetupTeams(TeamId);
        SetupReconciler(call => ValueTask.FromResult(call > 1));

        await Service.StartAsync(CancellationToken.None);
        await Calls[1].Task.WaitAsync(Deadline);
        await Service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTriggersKeepComing_RetriesAnUnsettledTeamAtItsDeadline()
    {
        var timeProvider = new FakeTimeProvider();
        var lease = OpenIdOptions.Team.ReconciliationLeaseDuration;
        var reconciled = Channel.CreateUnbounded<string>();
        var acmeCalls = 0;
        MockReconciler
            .Setup(x => x.ReconcileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(
                (string teamId, CancellationToken _) =>
                {
                    var settled = teamId != TeamId || Interlocked.Increment(ref acmeCalls) > 1;
                    reconciled.Writer.TryWrite(teamId);
                    return ValueTask.FromResult(settled);
                }
            );
        SetupPlatform();
        SetupTeams(TeamId);
        using var service = new TeamSettingsReconciliationService(
            MockRegistry.Object,
            MockPlatformProvider.Object,
            MockEnvironmentProvider.Object,
            MockStoreManagerFactory.Object,
            MockReconciler.Object,
            Trigger,
            Triggers,
            Options.Create(OpenIdOptions),
            timeProvider,
            CreateLooseMock<ILogger<TeamSettingsReconciliationService>>().Object
        );

        await service.StartAsync(CancellationToken.None);
        Assert.Equal(TeamId, await reconciled.Reader.ReadAsync().AsTask().WaitAsync(Deadline));

        // Each trigger arrives within the lease, and the clock reaches the deadline in quarters of it.
        for (var i = 0; i < 4; i++)
        {
            Trigger.Trigger(OtherTeamId);
            Assert.Equal(
                OtherTeamId,
                await reconciled.Reader.ReadAsync().AsTask().WaitAsync(Deadline)
            );
            timeProvider.Advance(lease / 4);
        }

        // The deadline has passed, so the unsettled team is attempted again whatever the triggers did.
        var next = await reconciled.Reader.ReadAsync().AsTask().WaitAsync(Deadline);
        await service.StopAsync(CancellationToken.None);

        Assert.Equal(TeamId, next);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheReconciliationFails_TriesAgainAfterTheLease()
    {
        SetupPlatform();
        SetupTeams(TeamId);
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
    public async Task StopAsync_BeforeThePlatformIsMaterialized_Completes()
    {
        // The host may run ExecuteAsync on another thread, so the stop waits until the service is waiting for the platform.
        var requested = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        MockEnvironmentProvider.Setup(x => x.Get()).Returns(MockEnvironment.Object).Verifiable();
        MockPlatformProvider
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
        throw new InvalidOperationException("The wait for the platform ended without a platform.");
    }

    // Replaces the source before it cancels the old one, so the producer never hands out a cancelled token again.
    private void SignalChange()
    {
        var previous = Changes;
        Changes = new CancellationTokenSource();
        previous.Cancel();
        previous.Dispose();
    }

    private void SetupPlatform()
    {
        SetupPlatformMembers();
        MockEnvironmentProvider.Setup(x => x.Get()).Returns(MockEnvironment.Object).Verifiable();
        MockPlatformProvider
            .Setup(x => x.GetAsync(MockEnvironment.Object, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockPlatform.Object)
            .Verifiable();
    }

    private void SetupPlatformMembers()
    {
        MockSettingsProvider
            .Setup(x => x.GetChangeToken())
            .Returns(() => new CancellationChangeToken(Changes.Token))
            .Verifiable();
        MockPlatform
            .Setup(x => x.SettingsProvider)
            .Returns(MockSettingsProvider.Object)
            .Verifiable();
    }

    // Counts the calls and completes the call's completion source after choosing its result.
    private void SetupReconciler(Func<int, ValueTask<bool>> result) =>
        MockReconciler
            .Setup(x => x.ReconcileAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(
                (string teamId, CancellationToken _) =>
                {
                    var call = Interlocked.Increment(ref _callCount);
                    ReconciledTeams.Add(teamId);
                    var outcome = result(call);
                    if (call <= Calls.Length)
                    {
                        Calls[call - 1].TrySetResult();
                    }

                    return outcome;
                }
            )
            .Verifiable();

    // The store lists the teams on one page, as it does for each unit of work.
    private void SetupTeams(params string[] teamIds)
    {
        MockStoreManagerFactory
            .Setup(x => x.CreateAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockStoreManager.Object)
            .Verifiable();
        MockStoreManager.Setup(x => x.GetStore<ITeamStore>()).Returns(MockTeamStore.Object);
        MockStoreManager.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
        MockTeamStore
            .Setup(x => x.GetPageAsync(It.IsAny<PageRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
                new PagedResult<PersistedTeam>
                {
                    Items = [.. teamIds.Select(CreateTeam)],
                    NextCursor = null,
                }
            )
            .Verifiable();
    }

    private static PersistedTeam CreateTeam(string teamId) =>
        new()
        {
            TeamId = teamId,
            DisplayName = teamId,
            IsDisabled = false,
            Settings = new PersistedTeamSettings
            {
                TeamId = teamId,
                Value = JsonElement.Parse("{}"),
            },
        };

    #endregion
}
