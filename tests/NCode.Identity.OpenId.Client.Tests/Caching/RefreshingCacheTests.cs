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


using NCode.Identity.OpenId.Client.Caching;
using Xunit;

namespace NCode.Identity.OpenId.Client.Tests.Caching;

public class RefreshingCacheTests : BaseTests
{
    private const string Key = "key";

    private static readonly RefreshingCachePolicy Policy = new(
        RefreshInterval: TimeSpan.FromHours(1),
        MinimumRefreshInterval: TimeSpan.FromMinutes(5),
        StaleLifetime: TimeSpan.FromHours(24)
    );

    private MutableTimeProvider TimeProvider { get; } = new(DateTimeOffset.UnixEpoch);

    private RefreshingCache<string> Cache { get; }

    private int FetchCount { get; set; }

    private bool ShouldFail { get; set; }

    public RefreshingCacheTests()
    {
        Cache = new RefreshingCache<string>(TimeProvider);
    }

    private ValueTask<string> FetchAsync(CancellationToken cancellationToken)
    {
        FetchCount++;
        if (ShouldFail)
            throw new OpenIdProviderException("fetch failed");
        return ValueTask.FromResult($"value-{FetchCount}");
    }

    private ValueTask<string> GetAsync() =>
        Cache.GetAsync(Key, Policy, FetchAsync, CancellationToken.None);

    private ValueTask<string> RefreshAsync() =>
        Cache.RefreshAsync(Key, Policy, FetchAsync, CancellationToken.None);

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WithinInterval_DoesNotFetch()
    {
        var first = await GetAsync();
        TimeProvider.UtcNow += TimeSpan.FromMinutes(30);
        var second = await GetAsync();

        Assert.Equal(1, FetchCount);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task GetAsync_AfterInterval_Fetches()
    {
        await GetAsync();
        TimeProvider.UtcNow += TimeSpan.FromHours(2);
        var second = await GetAsync();

        Assert.Equal(2, FetchCount);
        Assert.Equal("value-2", second);
    }

    [Fact]
    public async Task GetAsync_FailedRefreshWithinStaleLifetime_ReturnsStaleValue()
    {
        var first = await GetAsync();
        TimeProvider.UtcNow += TimeSpan.FromHours(2);
        ShouldFail = true;

        var second = await GetAsync();

        Assert.Equal(2, FetchCount);
        Assert.Equal(first, second);
    }

    [Fact]
    public async Task GetAsync_FailedRefreshAfterStaleLifetime_Throws()
    {
        await GetAsync();
        TimeProvider.UtcNow += TimeSpan.FromHours(2);
        ShouldFail = true;
        await GetAsync();

        TimeProvider.UtcNow += TimeSpan.FromHours(25);

        await Assert.ThrowsAsync<OpenIdProviderException>(async () => await GetAsync());
    }

    [Fact]
    public async Task GetAsync_AfterFailedRefreshWithinMinimumInterval_DoesNotFetch()
    {
        await GetAsync();
        TimeProvider.UtcNow += TimeSpan.FromHours(2);
        ShouldFail = true;
        await GetAsync();
        Assert.Equal(2, FetchCount);

        TimeProvider.UtcNow += TimeSpan.FromMinutes(1);
        await GetAsync();

        Assert.Equal(2, FetchCount);
    }

    [Fact]
    public async Task GetAsync_ConcurrentCalls_FetchOnce()
    {
        var completion = new TaskCompletionSource<string>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var fetchCount = 0;

        async ValueTask<string> SlowFetchAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref fetchCount);
            return await completion.Task;
        }

        var first = Cache.GetAsync(Key, Policy, SlowFetchAsync, CancellationToken.None).AsTask();
        var second = Cache.GetAsync(Key, Policy, SlowFetchAsync, CancellationToken.None).AsTask();
        completion.SetResult("shared");

        var results = await Task.WhenAll(first, second);

        Assert.Equal(1, fetchCount);
        Assert.Equal(["shared", "shared"], results);
    }

    [Fact]
    public async Task GetAsync_FirstFetchFails_ThrowsAndNextCallFetchesAgain()
    {
        ShouldFail = true;
        await Assert.ThrowsAsync<OpenIdProviderException>(async () => await GetAsync());

        ShouldFail = false;
        var value = await GetAsync();

        Assert.Equal(2, FetchCount);
        Assert.Equal("value-2", value);
    }

    #endregion

    #region RefreshAsync Tests

    [Fact]
    public async Task RefreshAsync_WithinMinimumInterval_DoesNotFetch()
    {
        await GetAsync();
        TimeProvider.UtcNow += TimeSpan.FromMinutes(1);

        await RefreshAsync();

        Assert.Equal(1, FetchCount);
    }

    [Fact]
    public async Task RefreshAsync_AfterMinimumInterval_Fetches()
    {
        await GetAsync();
        TimeProvider.UtcNow += TimeSpan.FromMinutes(6);

        var value = await RefreshAsync();

        Assert.Equal(2, FetchCount);
        Assert.Equal("value-2", value);
    }

    #endregion
}
