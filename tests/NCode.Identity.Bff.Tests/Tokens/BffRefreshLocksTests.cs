// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Tokens;
using Xunit;

namespace NCode.Identity.Bff.Tests.Tokens;

public class BffRefreshLocksTests : BaseTests
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(50);

    private const string Purpose = BffRefreshLocks.UserPurpose;

    private static async Task<IDisposable> EnterAsync(
        BffRefreshLocks locks,
        string key,
        string purpose = Purpose
    )
    {
        var lease = await locks.TryEnterAsync(purpose, key, WaitTimeout, CancellationToken.None);
        return Assert.IsAssignableFrom<IDisposable>(lease);
    }

    #region TryEnterAsync Tests

    [Fact]
    public async Task TryEnterAsync_SameKeyWhileHeld_WaitsUntilReleased()
    {
        var locks = new BffRefreshLocks();
        var first = await EnterAsync(locks, "session");

        var pending = locks
            .TryEnterAsync(Purpose, "session", WaitTimeout, CancellationToken.None)
            .AsTask();

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
    public async Task TryEnterAsync_SameKeyDifferentPurposes_EnterTogether()
    {
        var locks = new BffRefreshLocks();

        // the exchange service holds its lock on a session while the user token service takes its own
        using var exchange = await EnterAsync(locks, "session", BffRefreshLocks.ExchangePurpose);
        using var user = await EnterAsync(locks, "session", BffRefreshLocks.UserPurpose);

        Assert.Equal(2, locks.Count);
    }

    [Fact]
    public async Task TryEnterAsync_TimeoutWhileHeld_ReturnsNull()
    {
        var locks = new BffRefreshLocks();
        using var first = await EnterAsync(locks, "session");

        var second = await locks.TryEnterAsync(
            Purpose,
            "session",
            ShortTimeout,
            CancellationToken.None
        );

        Assert.Null(second);
        Assert.Equal(1, locks.Count);
    }

    [Fact]
    public async Task TryEnterAsync_AfterEveryHolderLeaves_RemovesTheEntry()
    {
        var locks = new BffRefreshLocks();
        var first = await EnterAsync(locks, "session");
        var pending = locks
            .TryEnterAsync(Purpose, "session", WaitTimeout, CancellationToken.None)
            .AsTask();

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
        var pending = locks
            .TryEnterAsync(Purpose, "session", WaitTimeout, CancellationToken.None)
            .AsTask();

        first.Dispose();
        first.Dispose();

        using var second = Assert.IsAssignableFrom<IDisposable>(
            await pending.WaitAsync(WaitTimeout)
        );
        var third = await locks.TryEnterAsync(
            Purpose,
            "session",
            ShortTimeout,
            CancellationToken.None
        );

        Assert.Null(third);
        Assert.Equal(1, locks.Count);
    }

    #endregion
}
