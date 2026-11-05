// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Linq.Expressions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence;
using NCode.Identity.SharedState;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Teams;

public sealed class TeamChangedHandlerTests : IDisposable
{
    private const string TeamId = "platform";

    private MockRepository Mocks { get; } = new(MockBehavior.Strict);
    private Mock<IOpenIdTeamCache> MockTeamCache { get; }
    private Mock<IOpenIdTenantCache> MockTenantCache { get; }
    private Mock<IReplicaBroadcaster> MockReplicaBroadcaster { get; }
    private Mock<ITeamSettingsReconciliationTrigger> MockTrigger { get; }
    private Mock<TimeProvider> MockTimeProvider { get; }
    private TeamChangedHandler Handler { get; }

    public TeamChangedHandlerTests()
    {
        MockTeamCache = Mocks.Create<IOpenIdTeamCache>();
        MockTenantCache = Mocks.Create<IOpenIdTenantCache>();
        MockReplicaBroadcaster = Mocks.Create<IReplicaBroadcaster>();
        MockTrigger = Mocks.Create<ITeamSettingsReconciliationTrigger>();
        MockTimeProvider = Mocks.Create<TimeProvider>();
        MockTimeProvider
            .Setup(CreateTimerExpression)
            .Returns(
                (TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
                    TimeProvider.System.CreateTimer(callback, state, dueTime, period)
            )
            .Verifiable();

        Handler = new TeamChangedHandler(
            MockTeamCache.Object,
            MockTenantCache.Object,
            MockReplicaBroadcaster.Object,
            MockTrigger.Object,
            MockTimeProvider.Object,
            NullLogger<TeamChangedHandler>.Instance
        );
    }

    public void Dispose()
    {
        Mocks.Verify();
    }

    private static Expression<Func<TimeProvider, ITimer>> CreateTimerExpression =>
        x =>
            x.CreateTimer(
                It.IsAny<TimerCallback>(),
                It.IsAny<object?>(),
                PostCommitWork.Timeout,
                Timeout.InfiniteTimeSpan
            );

    private void SetupEviction(List<string>? calls = null)
    {
        MockTeamCache
            .Setup(x =>
                x.RemoveAsync(TeamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Callback(() => calls?.Add("team"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTenantCache
            .Setup(x =>
                x.RemoveTeamAsync(TeamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Callback(() => calls?.Add("tenants"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
    }

    #region HandleAsync Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_EvictsTheTeamAndItsTenantsThenAnnounces(bool settingsChanged)
    {
        var calls = new List<string>();
        SetupEviction(calls);
        if (settingsChanged)
            MockTrigger.Setup(x => x.Trigger(TeamId)).Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    TeamChangeBroadcastHandler.ChannelName,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback(() => calls.Add("broadcast"))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(
            new TeamChangedCommand(TeamId, settingsChanged),
            CancellationToken.None
        );

        Assert.Equal(["team", "tenants", "broadcast"], calls);
    }

    [Fact]
    public async Task HandleAsync_WhenSettingsChanged_TriggersTheTeamsReconciliation()
    {
        SetupEviction();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    TeamChangeBroadcastHandler.ChannelName,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTrigger.Setup(x => x.Trigger(TeamId)).Verifiable();

        await Handler.HandleAsync(
            new TeamChangedCommand(TeamId, SettingsChanged: true),
            CancellationToken.None
        );

        MockTrigger.Verify(x => x.Trigger(TeamId), Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenTheAnnouncementFails_CompletesTheChange()
    {
        SetupEviction();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    TeamChangeBroadcastHandler.ChannelName,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException("The broadcast failed."))
            .Verifiable();

        await Handler.HandleAsync(
            new TeamChangedCommand(TeamId, SettingsChanged: false),
            CancellationToken.None
        );
    }

    [Fact]
    public async Task HandleAsync_WhenTheEvictionFails_StillAnnouncesTheChange()
    {
        MockTeamCache
            .Setup(x =>
                x.RemoveAsync(TeamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .ThrowsAsync(new InvalidOperationException("The eviction failed."))
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    TeamChangeBroadcastHandler.ChannelName,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Handler.HandleAsync(
                new TeamChangedCommand(TeamId, SettingsChanged: false),
                CancellationToken.None
            )
        );

        Assert.Equal("The eviction failed.", exception.Message);
    }

    [Fact]
    public async Task HandleAsync_BoundsTheEvictionAndTheAnnouncementByThePostCommitTimeout()
    {
        SetupEviction();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    TeamChangeBroadcastHandler.ChannelName,
                    TeamId,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(
            new TeamChangedCommand(TeamId, SettingsChanged: false),
            CancellationToken.None
        );

        MockTimeProvider.Verify(CreateTimerExpression, Times.Exactly(2));
    }

    [Fact]
    public async Task HandleAsync_WithAnotherCasing_EvictsWhateverSpellingTheCommandCarries()
    {
        const string spelling = "PLATFORM";
        MockTeamCache
            .Setup(x =>
                x.RemoveAsync(spelling, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTenantCache
            .Setup(x =>
                x.RemoveTeamAsync(spelling, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockReplicaBroadcaster
            .Setup(x =>
                x.PublishAsync(
                    TeamChangeBroadcastHandler.ChannelName,
                    spelling,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(
            new TeamChangedCommand(spelling, SettingsChanged: false),
            CancellationToken.None
        );
    }

    #endregion
}
