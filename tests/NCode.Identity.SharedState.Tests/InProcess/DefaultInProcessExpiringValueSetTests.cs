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

public sealed class DefaultInProcessExpiringValueSetTests : BaseTests
{
    private static DateTimeOffset Start { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset Now { get; set; } = Start;

    private DefaultInProcessExpiringValueSet CreateValueSet()
    {
        var mockTimeProvider = CreateStrictMock<TimeProvider>();
        mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(() => Now).Verifiable();
        return new DefaultInProcessExpiringValueSet(mockTimeProvider.Object);
    }

    private static async Task<bool[]> RunConcurrentlyAsync(Func<Task<bool>> operation)
    {
        const int Concurrency = 32;
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = Enumerable
            .Range(0, Concurrency)
            .Select(_ =>
                Task.Run(async () =>
                {
                    await start.Task;
                    return await operation();
                })
            )
            .ToList();
        start.SetResult();
        return await Task.WhenAll(attempts);
    }

    #region TryAddAsync Tests

    [Fact]
    public async Task TryAddAsync_WhenAbsent_ReturnsTrueAndStoresVersionOne()
    {
        var valueSet = CreateValueSet();

        var added = await valueSet.TryAddAsync(
            "key",
            new byte[] { 1, 2, 3 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        Assert.True(added);
        var result = await valueSet.GetAsync("key", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Value.Value.ToArray());
        Assert.Equal(1, result.Value.Version);
    }

    [Fact]
    public async Task TryAddAsync_WhenPresentAndUnexpired_ReturnsFalseAndKeepsTheValue()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        var added = await valueSet.TryAddAsync(
            "key",
            new byte[] { 2 },
            Start.AddMinutes(5),
            CancellationToken.None
        );

        Assert.False(added);
        var result = await valueSet.GetAsync("key", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 1 }, result.Value.Value.ToArray());
    }

    [Fact]
    public async Task TryAddAsync_WhenPresentButExpired_ReturnsTrue()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start.AddSeconds(10),
            CancellationToken.None
        );
        Now = Start.AddSeconds(10);

        var added = await valueSet.TryAddAsync(
            "key",
            new byte[] { 2 },
            Now.AddMinutes(1),
            CancellationToken.None
        );

        Assert.True(added);
        var result = await valueSet.GetAsync("key", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 2 }, result.Value.Value.ToArray());
        Assert.Equal(1, result.Value.Version);
    }

    [Fact]
    public async Task TryAddAsync_WhenExpiryNotInFuture_ReturnsFalseAndRecordsNothing()
    {
        var valueSet = CreateValueSet();

        var added = await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start,
            CancellationToken.None
        );

        Assert.False(added);
        Assert.Equal(0, valueSet.Count);
    }

    [Fact]
    public async Task TryAddAsync_WhenCallerChangesItsBuffer_KeepsTheStoredValue()
    {
        var valueSet = CreateValueSet();
        var buffer = new byte[] { 1, 2, 3 };
        await valueSet.TryAddAsync("key", buffer, Start.AddMinutes(1), CancellationToken.None);

        buffer[0] = 9;

        var result = await valueSet.GetAsync("key", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 1, 2, 3 }, result.Value.Value.ToArray());
    }

    [Fact]
    public async Task TryAddAsync_WhenConcurrentForSameKey_ExactlyOneSucceeds()
    {
        var valueSet = CreateValueSet();

        var results = await RunConcurrentlyAsync(async () =>
            await valueSet.TryAddAsync(
                "key",
                new byte[] { 1 },
                Start.AddMinutes(1),
                CancellationToken.None
            )
        );

        Assert.Single(results, added => added);
    }

    [Fact]
    public async Task TryAddAsync_WhenSweepIsDue_RemovesExpiredValues()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "expiring",
            new byte[] { 1 },
            Start.AddSeconds(10),
            CancellationToken.None
        );
        Now = Start + DefaultInProcessExpiringValueSet.SweepInterval;

        await valueSet.TryAddAsync(
            "live",
            new byte[] { 2 },
            Now.AddMinutes(1),
            CancellationToken.None
        );

        Assert.Equal(1, valueSet.Count);
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WhenAbsent_ReturnsNull()
    {
        var valueSet = CreateValueSet();

        var result = await valueSet.GetAsync("key", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WhenExpired_ReturnsNull()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start.AddMinutes(1),
            CancellationToken.None
        );
        Now = Start.AddMinutes(1);

        var result = await valueSet.GetAsync("key", CancellationToken.None);

        Assert.Null(result);
    }

    #endregion

    #region TryReplaceAsync Tests

    [Fact]
    public async Task TryReplaceAsync_WhenVersionMatches_ReplacesTheValueAndIncrementsTheVersion()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        var replaced = await valueSet.TryReplaceAsync(
            "key",
            1,
            new byte[] { 2 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        Assert.True(replaced);
        var result = await valueSet.GetAsync("key", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 2 }, result.Value.Value.ToArray());
        Assert.Equal(2, result.Value.Version);
    }

    [Fact]
    public async Task TryReplaceAsync_WhenVersionDiffers_ReturnsFalseAndKeepsTheValue()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        var replaced = await valueSet.TryReplaceAsync(
            "key",
            5,
            new byte[] { 2 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        Assert.False(replaced);
        var result = await valueSet.GetAsync("key", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 1 }, result.Value.Value.ToArray());
        Assert.Equal(1, result.Value.Version);
    }

    [Fact]
    public async Task TryReplaceAsync_WhenAbsent_ReturnsFalse()
    {
        var valueSet = CreateValueSet();

        var replaced = await valueSet.TryReplaceAsync(
            "key",
            1,
            new byte[] { 2 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        Assert.False(replaced);
    }

    [Fact]
    public async Task TryReplaceAsync_WhenExpired_ReturnsFalse()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start.AddSeconds(10),
            CancellationToken.None
        );
        Now = Start.AddSeconds(10);

        var replaced = await valueSet.TryReplaceAsync(
            "key",
            1,
            new byte[] { 2 },
            Now.AddMinutes(1),
            CancellationToken.None
        );

        Assert.False(replaced);
    }

    [Fact]
    public async Task TryReplaceAsync_WhenExpiryNotInFuture_ReturnsFalseAndKeepsTheValue()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        var replaced = await valueSet.TryReplaceAsync(
            "key",
            1,
            new byte[] { 2 },
            Start,
            CancellationToken.None
        );

        Assert.False(replaced);
        var result = await valueSet.GetAsync("key", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(new byte[] { 1 }, result.Value.Value.ToArray());
        Assert.Equal(1, result.Value.Version);
    }

    [Fact]
    public async Task TryReplaceAsync_WhenExpiryIsLater_ExtendsTheExpiry()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start.AddMinutes(1),
            CancellationToken.None
        );
        await valueSet.TryReplaceAsync(
            "key",
            1,
            new byte[] { 2 },
            Start.AddMinutes(10),
            CancellationToken.None
        );
        Now = Start.AddMinutes(5);

        var result = await valueSet.GetAsync("key", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(new byte[] { 2 }, result.Value.Value.ToArray());
    }

    [Fact]
    public async Task TryReplaceAsync_WhenConcurrentForSameVersion_ExactlyOneSucceeds()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        var results = await RunConcurrentlyAsync(async () =>
            await valueSet.TryReplaceAsync(
                "key",
                1,
                new byte[] { 2 },
                Start.AddMinutes(1),
                CancellationToken.None
            )
        );

        Assert.Single(results, replaced => replaced);
        var result = await valueSet.GetAsync("key", CancellationToken.None);
        Assert.NotNull(result);
        Assert.Equal(2, result.Value.Version);
    }

    #endregion

    #region TryTakeAsync Tests

    [Fact]
    public async Task TryTakeAsync_WhenPresent_ReturnsTheValueAndRemovesIt()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1, 2 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        var result = await valueSet.TryTakeAsync("key", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(new byte[] { 1, 2 }, result.Value.Value.ToArray());
        Assert.Equal(1, result.Value.Version);
        Assert.Null(await valueSet.GetAsync("key", CancellationToken.None));
    }

    [Fact]
    public async Task TryTakeAsync_WhenAbsent_ReturnsNull()
    {
        var valueSet = CreateValueSet();

        var result = await valueSet.TryTakeAsync("key", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryTakeAsync_WhenExpired_ReturnsNull()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start.AddMinutes(1),
            CancellationToken.None
        );
        Now = Start.AddMinutes(1);

        var result = await valueSet.TryTakeAsync("key", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task TryTakeAsync_WhenConcurrentForSameKey_ExactlyOneReturnsTheValue()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        var results = await RunConcurrentlyAsync(async () =>
            await valueSet.TryTakeAsync("key", CancellationToken.None) is not null
        );

        Assert.Single(results, taken => taken);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_WhenPresent_RemovesIt()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "key",
            new byte[] { 1 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        await valueSet.RemoveAsync("key", CancellationToken.None);

        Assert.Null(await valueSet.GetAsync("key", CancellationToken.None));
        Assert.Equal(0, valueSet.Count);
    }

    [Fact]
    public async Task RemoveAsync_WhenAbsent_DoesNothing()
    {
        var valueSet = CreateValueSet();
        await valueSet.TryAddAsync(
            "other",
            new byte[] { 1 },
            Start.AddMinutes(1),
            CancellationToken.None
        );

        await valueSet.RemoveAsync("key", CancellationToken.None);

        Assert.NotNull(await valueSet.GetAsync("other", CancellationToken.None));
        Assert.Equal(1, valueSet.Count);
    }

    #endregion
}
