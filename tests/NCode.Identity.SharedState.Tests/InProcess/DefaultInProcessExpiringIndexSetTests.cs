// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Time.Testing;
using NCode.Identity.SharedState.InProcess;
using Xunit;

namespace NCode.Identity.SharedState.Tests.InProcess;

public sealed class DefaultInProcessExpiringIndexSetTests : BaseTests
{
    private static DateTimeOffset Start { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private FakeTimeProvider TimeProvider { get; } = new(Start);

    private DefaultInProcessExpiringIndexSet CreateIndexSet() => new(TimeProvider);

    private static async Task<string[]> GetSortedMembersAsync(
        DefaultInProcessExpiringIndexSet indexSet,
        string indexKey
    )
    {
        var members = await indexSet.GetMembersAsync(indexKey, CancellationToken.None);
        return members.Order(StringComparer.Ordinal).ToArray();
    }

    #region AddMemberAsync Tests

    [Fact]
    public async Task AddMemberAsync_ThenGetMembersAsync_ReturnsTheMember()
    {
        var indexSet = CreateIndexSet();

        await indexSet.AddMemberAsync("index", "a", Start.AddMinutes(1), CancellationToken.None);

        Assert.Equal(["a"], await GetSortedMembersAsync(indexSet, "index"));
    }

    [Fact]
    public async Task AddMemberAsync_WhenExpiryNotInFuture_AddsNothing()
    {
        var indexSet = CreateIndexSet();

        await indexSet.AddMemberAsync("index", "a", Start, CancellationToken.None);

        Assert.Equal(0, indexSet.Count);
        Assert.Empty(await GetSortedMembersAsync(indexSet, "index"));
    }

    [Fact]
    public async Task AddMemberAsync_WhenMemberPresent_SetsItsExpiry()
    {
        var indexSet = CreateIndexSet();
        await indexSet.AddMemberAsync("index", "a", Start.AddMinutes(1), CancellationToken.None);
        await indexSet.AddMemberAsync("index", "a", Start.AddMinutes(10), CancellationToken.None);
        TimeProvider.SetUtcNow(Start.AddMinutes(5));

        Assert.Equal(["a"], await GetSortedMembersAsync(indexSet, "index"));
    }

    [Fact]
    public async Task AddMemberAsync_WhenConcurrentForSameIndex_KeepsEveryMember()
    {
        var indexSet = CreateIndexSet();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var expected = Enumerable.Range(0, 32).Select(i => $"member-{i:D2}").ToArray();

        var tasks = expected
            .Select(member =>
                Task.Run(async () =>
                {
                    await start.Task;
                    await indexSet.AddMemberAsync(
                        "index",
                        member,
                        Start.AddMinutes(1),
                        CancellationToken.None
                    );
                })
            )
            .ToList();
        start.SetResult();
        await Task.WhenAll(tasks);

        Assert.Equal(expected, await GetSortedMembersAsync(indexSet, "index"));
    }

    [Fact]
    public async Task AddMemberAsync_WhenSweepIsDue_RemovesIndexesWithNoLiveMember()
    {
        var indexSet = CreateIndexSet();
        await indexSet.AddMemberAsync(
            "expiring",
            "a",
            Start.AddSeconds(10),
            CancellationToken.None
        );
        TimeProvider.SetUtcNow(Start + DefaultInProcessExpiringIndexSet.SweepInterval);

        await indexSet.AddMemberAsync(
            "live",
            "b",
            TimeProvider.GetUtcNow().AddMinutes(1),
            CancellationToken.None
        );

        Assert.Equal(1, indexSet.Count);
        Assert.Equal(["b"], await GetSortedMembersAsync(indexSet, "live"));
    }

    #endregion

    #region GetMembersAsync Tests

    [Fact]
    public async Task GetMembersAsync_WhenIndexAbsent_ReturnsEmpty()
    {
        var indexSet = CreateIndexSet();

        Assert.Empty(await indexSet.GetMembersAsync("index", CancellationToken.None));
    }

    [Fact]
    public async Task GetMembersAsync_OmitsExpiredMembers()
    {
        var indexSet = CreateIndexSet();
        await indexSet.AddMemberAsync("index", "a", Start.AddSeconds(10), CancellationToken.None);
        await indexSet.AddMemberAsync("index", "b", Start.AddMinutes(1), CancellationToken.None);
        TimeProvider.SetUtcNow(Start.AddSeconds(10));

        Assert.Equal(["b"], await GetSortedMembersAsync(indexSet, "index"));
    }

    [Fact]
    public async Task GetMembersAsync_WhenMembersDifferOnlyByCase_ReturnsBoth()
    {
        var indexSet = CreateIndexSet();
        await indexSet.AddMemberAsync("index", "a", Start.AddMinutes(1), CancellationToken.None);
        await indexSet.AddMemberAsync("index", "A", Start.AddMinutes(1), CancellationToken.None);

        Assert.Equal(["A", "a"], await GetSortedMembersAsync(indexSet, "index"));
    }

    #endregion

    #region RemoveMemberAsync Tests

    [Fact]
    public async Task RemoveMemberAsync_RemovesOnlyThatMember()
    {
        var indexSet = CreateIndexSet();
        await indexSet.AddMemberAsync("index", "a", Start.AddMinutes(1), CancellationToken.None);
        await indexSet.AddMemberAsync("index", "b", Start.AddMinutes(1), CancellationToken.None);

        await indexSet.RemoveMemberAsync("index", "a", CancellationToken.None);

        Assert.Equal(["b"], await GetSortedMembersAsync(indexSet, "index"));
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_RemovesEveryMember()
    {
        var indexSet = CreateIndexSet();
        await indexSet.AddMemberAsync("index", "a", Start.AddMinutes(1), CancellationToken.None);
        await indexSet.AddMemberAsync("index", "b", Start.AddMinutes(1), CancellationToken.None);

        await indexSet.RemoveAsync("index", CancellationToken.None);

        Assert.Empty(await GetSortedMembersAsync(indexSet, "index"));
        Assert.Equal(0, indexSet.Count);
    }

    #endregion
}
