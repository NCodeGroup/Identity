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

using NCode.Identity.SharedState.InProcess;
using Xunit;

namespace NCode.Identity.SharedState.Tests.InProcess;

public sealed class DefaultInProcessExpiringIndexSetTests : BaseTests
{
    private static DateTimeOffset Start { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset Now { get; set; } = Start;

    private DefaultInProcessExpiringIndexSet CreateIndexSet()
    {
        var mockTimeProvider = CreateStrictMock<TimeProvider>();
        mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(() => Now);
        return new DefaultInProcessExpiringIndexSet(mockTimeProvider.Object);
    }

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
        Now = Start.AddMinutes(5);

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
        Now = Start + DefaultInProcessExpiringIndexSet.SweepInterval;

        await indexSet.AddMemberAsync("live", "b", Now.AddMinutes(1), CancellationToken.None);

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
        Now = Start.AddSeconds(10);

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
