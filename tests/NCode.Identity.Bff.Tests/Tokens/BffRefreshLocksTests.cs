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

using NCode.Identity.Bff.Tokens;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class BffRefreshLocksTests : BaseTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(50);

    private static async Task<IDisposable> EnterAsync(BffRefreshLocks locks, string storeKey)
    {
        var lease = await locks.TryEnterAsync(storeKey, WaitTimeout, CancellationToken.None);
        return Assert.IsAssignableFrom<IDisposable>(lease);
    }

    #region TryEnterAsync Tests

    [Fact]
    public async Task TryEnterAsync_SameKeyWhileHeld_WaitsUntilReleased()
    {
        var locks = new BffRefreshLocks();
        var first = await EnterAsync(locks, "session");

        var pending = locks.TryEnterAsync("session", WaitTimeout, CancellationToken.None).AsTask();

        Assert.False(pending.IsCompleted);

        first.Dispose();

        var second = await pending.WaitAsync(WaitTimeout);

        Assert.NotNull(second);
        second.Dispose();
    }

    [Fact]
    public async Task TryEnterAsync_DifferentKeys_EnterTogether()
    {
        var locks = new BffRefreshLocks();

        using var first = await EnterAsync(locks, "session-a");
        using var second = await EnterAsync(locks, "session-b");

        Assert.Equal(2, locks.Count);
    }

    [Fact]
    public async Task TryEnterAsync_TimeoutWhileHeld_ReturnsNull()
    {
        var locks = new BffRefreshLocks();
        using var first = await EnterAsync(locks, "session");

        var second = await locks.TryEnterAsync("session", ShortTimeout, CancellationToken.None);

        Assert.Null(second);
        Assert.Equal(1, locks.Count);
    }

    [Fact]
    public async Task TryEnterAsync_AfterEveryHolderLeaves_RemovesTheEntry()
    {
        var locks = new BffRefreshLocks();
        var first = await EnterAsync(locks, "session");
        var pending = locks.TryEnterAsync("session", WaitTimeout, CancellationToken.None).AsTask();

        first.Dispose();
        var second = Assert.IsAssignableFrom<IDisposable>(await pending.WaitAsync(WaitTimeout));

        Assert.Equal(1, locks.Count);

        second.Dispose();

        Assert.Equal(0, locks.Count);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task Dispose_Twice_ReleasesOnce()
    {
        var locks = new BffRefreshLocks();
        var first = await EnterAsync(locks, "session");
        var pending = locks.TryEnterAsync("session", WaitTimeout, CancellationToken.None).AsTask();

        first.Dispose();
        first.Dispose();

        using var second = Assert.IsAssignableFrom<IDisposable>(
            await pending.WaitAsync(WaitTimeout)
        );
        var third = await locks.TryEnterAsync("session", ShortTimeout, CancellationToken.None);

        Assert.Null(third);
        Assert.Equal(1, locks.Count);
    }

    #endregion
}
