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

public sealed class DefaultInProcessExpiringCounterSetTests : BaseTests
{
    private static DateTimeOffset Start { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset Now { get; set; } = Start;

    private DefaultInProcessExpiringCounterSet CreateCounterSet()
    {
        var mockTimeProvider = CreateStrictMock<TimeProvider>();
        mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(() => Now).Verifiable();
        return new DefaultInProcessExpiringCounterSet(mockTimeProvider.Object);
    }

    #region IncrementAsync Tests

    [Fact]
    public async Task IncrementAsync_WhenAbsent_StartsAtOne()
    {
        var counterSet = CreateCounterSet();

        var count = await counterSet.IncrementAsync(
            "key",
            Start.AddMinutes(1),
            CancellationToken.None
        );

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task IncrementAsync_WhenPresentAndUnexpired_ReturnsTheNextCount()
    {
        var counterSet = CreateCounterSet();
        await counterSet.IncrementAsync("key", Start.AddMinutes(1), CancellationToken.None);
        await counterSet.IncrementAsync("key", Start.AddMinutes(1), CancellationToken.None);

        var count = await counterSet.IncrementAsync(
            "key",
            Start.AddMinutes(1),
            CancellationToken.None
        );

        Assert.Equal(3, count);
    }

    [Fact]
    public async Task IncrementAsync_WhenPresent_KeepsTheFirstExpiry()
    {
        var counterSet = CreateCounterSet();
        await counterSet.IncrementAsync("key", Start.AddSeconds(10), CancellationToken.None);
        await counterSet.IncrementAsync("key", Start.AddMinutes(5), CancellationToken.None);
        Now = Start.AddSeconds(10);

        var count = await counterSet.GetAsync("key", CancellationToken.None);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task IncrementAsync_WhenPresentButExpired_StartsAgainAtOne()
    {
        var counterSet = CreateCounterSet();
        await counterSet.IncrementAsync("key", Start.AddSeconds(10), CancellationToken.None);
        await counterSet.IncrementAsync("key", Start.AddSeconds(10), CancellationToken.None);
        Now = Start.AddSeconds(10);

        var count = await counterSet.IncrementAsync(
            "key",
            Now.AddMinutes(1),
            CancellationToken.None
        );

        Assert.Equal(1, count);
        Assert.Equal(1, await counterSet.GetAsync("key", CancellationToken.None));
    }

    [Fact]
    public async Task IncrementAsync_WhenExpiryNotInFuture_ReturnsZeroAndCountsNothing()
    {
        var counterSet = CreateCounterSet();

        var count = await counterSet.IncrementAsync("key", Start, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Equal(0, counterSet.Count);
    }

    [Fact]
    public async Task IncrementAsync_KeepsKeysApartOrdinally()
    {
        var counterSet = CreateCounterSet();
        await counterSet.IncrementAsync("key", Start.AddMinutes(1), CancellationToken.None);

        var count = await counterSet.IncrementAsync(
            "KEY",
            Start.AddMinutes(1),
            CancellationToken.None
        );

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task IncrementAsync_WhenCalledConcurrently_CountsEveryCall()
    {
        const int calls = 64;
        var counterSet = CreateCounterSet();

        var counts = await Task.WhenAll(
            Enumerable
                .Range(0, calls)
                .Select(_ =>
                    Task.Run(async () =>
                        await counterSet.IncrementAsync(
                            "key",
                            Start.AddMinutes(1),
                            CancellationToken.None
                        )
                    )
                )
        );

        Assert.Equal(Enumerable.Range(1, calls).Select(x => (long)x), counts.Order());
    }

    [Fact]
    public async Task IncrementAsync_WhenSweepIsDue_RemovesExpiredCounters()
    {
        var counterSet = CreateCounterSet();
        await counterSet.IncrementAsync("old", Start.AddSeconds(10), CancellationToken.None);
        Now = Start + DefaultInProcessExpiringCounterSet.SweepInterval;

        await counterSet.IncrementAsync("new", Now.AddMinutes(1), CancellationToken.None);

        Assert.Equal(1, counterSet.Count);
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WhenAbsent_ReturnsZero()
    {
        var counterSet = CreateCounterSet();

        var count = await counterSet.GetAsync("key", CancellationToken.None);

        Assert.Equal(0, count);
    }

    [Fact]
    public async Task GetAsync_WhenPresent_ReturnsTheCountWithoutIncrementing()
    {
        var counterSet = CreateCounterSet();
        await counterSet.IncrementAsync("key", Start.AddMinutes(1), CancellationToken.None);
        await counterSet.IncrementAsync("key", Start.AddMinutes(1), CancellationToken.None);

        await counterSet.GetAsync("key", CancellationToken.None);
        var count = await counterSet.GetAsync("key", CancellationToken.None);

        Assert.Equal(2, count);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WhenPresent_StartsTheCountAgain()
    {
        var counterSet = CreateCounterSet();
        await counterSet.IncrementAsync("key", Start.AddMinutes(1), CancellationToken.None);
        await counterSet.IncrementAsync("key", Start.AddMinutes(1), CancellationToken.None);

        await counterSet.RemoveAsync("key", CancellationToken.None);
        var count = await counterSet.IncrementAsync(
            "key",
            Start.AddMinutes(1),
            CancellationToken.None
        );

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task RemoveAsync_WhenAbsent_DoesNothing()
    {
        var counterSet = CreateCounterSet();

        await counterSet.RemoveAsync("key", CancellationToken.None);
        var count = await counterSet.GetAsync("key", CancellationToken.None);

        Assert.Equal(0, count);
    }

    #endregion
}
