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

public sealed class DefaultInProcessExpiringKeySetTests : BaseTests
{
    private static DateTimeOffset Start { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private DateTimeOffset Now { get; set; } = Start;

    private DefaultInProcessExpiringKeySet CreateKeySet()
    {
        var mockTimeProvider = CreateStrictMock<TimeProvider>();
        mockTimeProvider.Setup(x => x.GetUtcNow()).Returns(() => Now).Verifiable();
        return new DefaultInProcessExpiringKeySet(mockTimeProvider.Object);
    }

    #region TryAddAsync Tests

    [Fact]
    public async Task TryAddAsync_WhenAbsent_ReturnsTrue()
    {
        var keySet = CreateKeySet();

        var added = await keySet.TryAddAsync("key", Start.AddMinutes(1), CancellationToken.None);

        Assert.True(added);
    }

    [Fact]
    public async Task TryAddAsync_WhenPresentAndUnexpired_ReturnsFalse()
    {
        var keySet = CreateKeySet();
        await keySet.TryAddAsync("key", Start.AddMinutes(1), CancellationToken.None);

        var added = await keySet.TryAddAsync("key", Start.AddMinutes(5), CancellationToken.None);

        Assert.False(added);
    }

    [Fact]
    public async Task TryAddAsync_WhenPresentButExpired_ReturnsTrue()
    {
        var keySet = CreateKeySet();
        await keySet.TryAddAsync("key", Start.AddSeconds(10), CancellationToken.None);
        Now = Start.AddSeconds(10);

        var added = await keySet.TryAddAsync("key", Now.AddMinutes(1), CancellationToken.None);

        Assert.True(added);
        Assert.True(await keySet.ContainsAsync("key", CancellationToken.None));
    }

    [Fact]
    public async Task TryAddAsync_WhenExpiryNotInFuture_ReturnsFalseAndRecordsNothing()
    {
        var keySet = CreateKeySet();

        var added = await keySet.TryAddAsync("key", Start, CancellationToken.None);

        Assert.False(added);
        Assert.Equal(0, keySet.Count);
    }

    [Fact]
    public async Task TryAddAsync_WhenKeysDifferOnlyByCase_TreatsThemAsDistinct()
    {
        var keySet = CreateKeySet();
        await keySet.TryAddAsync("key", Start.AddMinutes(1), CancellationToken.None);

        var added = await keySet.TryAddAsync("KEY", Start.AddMinutes(1), CancellationToken.None);

        Assert.True(added);
    }

    [Fact]
    public async Task TryAddAsync_WhenConcurrentForSameKey_ExactlyOneSucceeds()
    {
        const int Concurrency = 32;
        var keySet = CreateKeySet();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var attempts = Enumerable
            .Range(0, Concurrency)
            .Select(_ =>
                Task.Run(async () =>
                {
                    await start.Task;
                    return await keySet.TryAddAsync(
                        "key",
                        Start.AddMinutes(1),
                        CancellationToken.None
                    );
                })
            )
            .ToList();
        start.SetResult();
        var results = await Task.WhenAll(attempts);

        Assert.Single(results, added => added);
    }

    [Fact]
    public async Task TryAddAsync_WhenSweepIsDue_RemovesExpiredEntries()
    {
        var keySet = CreateKeySet();
        await keySet.TryAddAsync("expiring", Start.AddSeconds(10), CancellationToken.None);
        Now = Start + DefaultInProcessExpiringKeySet.SweepInterval;

        await keySet.TryAddAsync("live", Now.AddMinutes(1), CancellationToken.None);

        Assert.Equal(1, keySet.Count);
    }

    [Fact]
    public async Task TryAddAsync_WhenSweepIsNotDue_KeepsExpiredEntries()
    {
        var keySet = CreateKeySet();
        await keySet.TryAddAsync("expiring", Start.AddSeconds(10), CancellationToken.None);
        Now = Start.AddSeconds(30);

        await keySet.TryAddAsync("live", Now.AddMinutes(1), CancellationToken.None);

        Assert.Equal(2, keySet.Count);
        Assert.False(await keySet.ContainsAsync("expiring", CancellationToken.None));
    }

    #endregion

    #region ContainsAsync Tests

    [Fact]
    public async Task ContainsAsync_WhenPresentAndUnexpired_ReturnsTrue()
    {
        var keySet = CreateKeySet();
        await keySet.TryAddAsync("key", Start.AddMinutes(1), CancellationToken.None);

        var contains = await keySet.ContainsAsync("key", CancellationToken.None);

        Assert.True(contains);
    }

    [Fact]
    public async Task ContainsAsync_WhenExpired_ReturnsFalse()
    {
        var keySet = CreateKeySet();
        await keySet.TryAddAsync("key", Start.AddMinutes(1), CancellationToken.None);
        Now = Start.AddMinutes(1);

        var contains = await keySet.ContainsAsync("key", CancellationToken.None);

        Assert.False(contains);
    }

    [Fact]
    public async Task ContainsAsync_WhenAbsent_ReturnsFalse()
    {
        var keySet = CreateKeySet();

        var contains = await keySet.ContainsAsync("key", CancellationToken.None);

        Assert.False(contains);
    }

    #endregion
}
