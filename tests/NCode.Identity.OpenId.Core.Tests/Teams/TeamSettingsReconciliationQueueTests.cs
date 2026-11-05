// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Teams;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Teams;

public sealed class TeamSettingsReconciliationQueueTests
{
    private static TimeSpan Deadline { get; } = TimeSpan.FromSeconds(5);

    private TeamSettingsReconciliationQueue Queue { get; } = new();

    #region TakeAsync Tests

    [Fact]
    public async Task TakeAsync_WhenTeamsAreAdded_ReturnsEachTeamOnce()
    {
        Queue.Add("acme");
        Queue.Add("ACME");
        Queue.Add("globex");

        var request = await Queue.TakeAsync(CancellationToken.None);

        Assert.False(request.EveryTeam);
        Assert.Equal(2, request.TeamIds.Count);
    }

    [Fact]
    public async Task TakeAsync_WhenEveryTeamIsAdded_CoversThePendingTeams()
    {
        Queue.Add("acme");
        Queue.Add(null);
        Queue.Add("globex");

        var request = await Queue.TakeAsync(CancellationToken.None);

        Assert.True(request.EveryTeam);
        Assert.Empty(request.TeamIds);
    }

    [Fact]
    public async Task TakeAsync_WhenManyTeamsAreAdded_ReturnsEveryTeam()
    {
        const int TeamCount = 1000;
        for (var i = 0; i < TeamCount; ++i)
            Queue.Add($"team-{i:D4}");

        var request = await Queue.TakeAsync(CancellationToken.None);

        Assert.False(request.EveryTeam);
        Assert.Equal(TeamCount, request.TeamIds.Count);
    }

    [Fact]
    public async Task TakeAsync_WhenNothingIsPending_WaitsForAnAdd()
    {
        var task = Queue.TakeAsync(CancellationToken.None).AsTask();

        Assert.False(task.IsCompleted);

        Queue.Add("acme");
        var request = await task.WaitAsync(Deadline);

        Assert.False(request.EveryTeam);
        Assert.Equal(["acme"], request.TeamIds);
    }

    [Fact]
    public async Task TakeAsync_AfterATake_ReturnsOnlyLaterRequests()
    {
        Queue.Add("a");
        await Queue.TakeAsync(CancellationToken.None);
        Queue.Add("b");

        var request = await Queue.TakeAsync(CancellationToken.None);

        Assert.False(request.EveryTeam);
        Assert.Equal(["b"], request.TeamIds);
    }

    [Fact]
    public async Task TakeAsync_WhenCancelledWhileWaiting_KeepsLaterRequests()
    {
        using var cts = new CancellationTokenSource();
        var task = Queue.TakeAsync(cts.Token).AsTask();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task.WaitAsync(Deadline));

        Queue.Add("acme");
        var request = await Queue.TakeAsync(CancellationToken.None);

        Assert.Equal(["acme"], request.TeamIds);
    }

    #endregion
}
