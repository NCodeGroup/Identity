// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Caching.Memory;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.OutboundHttp;

public sealed class SingleFlightTests : BaseTests
{
    #region Scaffold

    private const string Key = "key";
    private const string Value = "value";

    private MemoryCache MemoryCache { get; } = new(new MemoryCacheOptions());

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            MemoryCache.Dispose();
        }

        base.Dispose(disposing);
    }

    private SingleFlight<string> CreateSingleFlight() => new(MemoryCache, TimeSpan.FromMinutes(1));

    #endregion

    #region RunAsync Tests

    [Fact]
    public async Task RunAsync_WhenTwoCallersAskAtOnce_RetrievesOnce()
    {
        var calls = 0;
        var singleFlight = CreateSingleFlight();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async ValueTask<string> Retrieve(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return Value;
        }

        var first = singleFlight.RunAsync(Key, Retrieve, CancellationToken.None).AsTask();
        var second = singleFlight.RunAsync(Key, Retrieve, CancellationToken.None).AsTask();
        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.Equal(Value, result));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RunAsync_WhenTheFirstCallerGivesUp_CompletesTheRetrievalForTheOthers()
    {
        var calls = 0;
        var singleFlight = CreateSingleFlight();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async ValueTask<string> Retrieve(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return Value;
        }
        using var cancellation = new CancellationTokenSource();

        var first = singleFlight.RunAsync(Key, Retrieve, cancellation.Token).AsTask();
        await cancellation.CancelAsync();
        var second = singleFlight.RunAsync(Key, Retrieve, CancellationToken.None).AsTask();
        gate.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(Value, await second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task RunAsync_WhenTheRetrievalHasEnded_RetrievesAgain()
    {
        var calls = 0;
        var singleFlight = CreateSingleFlight();
        ValueTask<string> Retrieve(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return ValueTask.FromResult(Value);
        }

        var first = await singleFlight.RunAsync(Key, Retrieve, CancellationToken.None);
        var second = await singleFlight.RunAsync(Key, Retrieve, CancellationToken.None);

        Assert.Equal(Value, first);
        Assert.Equal(Value, second);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task RunAsync_WhenTheRetrievalThrows_ThrowsAndRetrievesAgainNextTime()
    {
        var calls = 0;
        var singleFlight = CreateSingleFlight();
        ValueTask<string> Retrieve(CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref calls) == 1)
                return ValueTask.FromException<string>(new InvalidOperationException());

            return ValueTask.FromResult(Value);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await singleFlight.RunAsync(Key, Retrieve, CancellationToken.None)
        );
        var second = await singleFlight.RunAsync(Key, Retrieve, CancellationToken.None);

        Assert.Equal(Value, second);
        Assert.Equal(2, calls);
    }

    #endregion

    #region FailureCacheDuration Tests

    [Fact]
    public void FailureCacheDuration_ReturnsTheDurationGivenToTheConstructor()
    {
        var singleFlight = CreateSingleFlight();

        Assert.Equal(TimeSpan.FromMinutes(1), singleFlight.FailureCacheDuration);
    }

    #endregion

    #region IsFailureRemembered Tests

    [Fact]
    public void IsFailureRemembered_WhenNoFailureIsRemembered_ReturnsFalse()
    {
        var singleFlight = CreateSingleFlight();

        var result = singleFlight.IsFailureRemembered(Key);

        Assert.False(result);
    }

    #endregion

    #region RememberFailure Tests

    [Fact]
    public void RememberFailure_RemembersTheFailureUnderTheUnavailableKey()
    {
        var singleFlight = CreateSingleFlight();

        singleFlight.RememberFailure(Key);

        Assert.True(singleFlight.IsFailureRemembered(Key));
        Assert.True(MemoryCache.TryGetValue("key:unavailable", out _));
        Assert.False(singleFlight.IsFailureRemembered("other"));
    }

    #endregion
}
