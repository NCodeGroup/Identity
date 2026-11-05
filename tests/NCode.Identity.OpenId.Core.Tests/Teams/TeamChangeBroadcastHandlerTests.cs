// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.OpenId.Teams;
using NCode.Identity.OpenId.Tenants;
using NCode.PropertyBag;
using Xunit;

namespace NCode.Identity.OpenId.Core.Teams;

public sealed class TeamChangeBroadcastHandlerTests : IDisposable
{
    private const string TeamId = "platform";

    private MockRepository Mocks { get; } = new(MockBehavior.Strict);
    private Mock<IOpenIdTeamCache> MockTeamCache { get; }
    private Mock<IOpenIdTenantCache> MockTenantCache { get; }
    private TeamChangeBroadcastHandler Handler { get; }

    public TeamChangeBroadcastHandlerTests()
    {
        MockTeamCache = Mocks.Create<IOpenIdTeamCache>();
        MockTenantCache = Mocks.Create<IOpenIdTenantCache>();
        Handler = new TeamChangeBroadcastHandler(
            MockTeamCache.Object,
            MockTenantCache.Object,
            NullLogger<TeamChangeBroadcastHandler>.Instance
        );
    }

    public void Dispose()
    {
        Mocks.Verify();
    }

    #region Channel Tests

    [Fact]
    public void Channel_IsTheTeamChangedChannel()
    {
        Assert.Equal("team-changed", Handler.Channel);
        Assert.Equal(TeamChangeBroadcastHandler.ChannelName, Handler.Channel);
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_EvictsTheAnnouncedTeamAndItsTenants()
    {
        MockTeamCache
            .Setup(x =>
                x.RemoveAsync(TeamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTenantCache
            .Setup(x =>
                x.RemoveTeamAsync(TeamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleAsync(TeamId, CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenTheTeamRemovalFails_StillEvictsTheTenantsAndThrows()
    {
        var failure = new InvalidOperationException("The removal failed.");
        MockTeamCache
            .Setup(x =>
                x.RemoveAsync(TeamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(() => ValueTask.FromException(failure))
            .Verifiable();
        MockTenantCache
            .Setup(x =>
                x.RemoveTeamAsync(TeamId, It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        var thrown = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Handler.HandleAsync(TeamId, CancellationToken.None)
        );

        Assert.Same(failure, thrown);
    }

    #endregion

    #region HandleMissedMessagesAsync Tests

    [Fact]
    public async Task HandleMissedMessagesAsync_ClearsBothCaches()
    {
        MockTeamCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockTenantCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenTheTeamCacheClearFails_StillClearsTheTenantCache()
    {
        MockTeamCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The clear failed."))
            .Verifiable();
        MockTenantCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    [Fact]
    public async Task HandleMissedMessagesAsync_WhenBothClearsFail_LogsAndCompletes()
    {
        MockTeamCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The clear failed."))
            .Verifiable();
        MockTenantCache
            .Setup(x => x.ClearAsync(It.IsAny<IPropertyBag>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The clear failed."))
            .Verifiable();

        await Handler.HandleMissedMessagesAsync(CancellationToken.None);
    }

    #endregion
}
