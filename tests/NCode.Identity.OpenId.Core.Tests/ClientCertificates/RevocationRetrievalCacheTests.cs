// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public class RevocationRetrievalCacheTests : BaseTests
{
    #region Scaffold

    private const string Key = "key";
    private const string Location = "http://crl.example.test/root.crl";
    private const string Subject = "CN=c";
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

    private RevocationRetrievalCache CreateCache() => CreateCacheOver(MemoryCache);

    private RevocationRetrievalCache CreateCacheOver(IMemoryCache memoryCache) =>
        new(
            memoryCache,
            Options.Create(new ClientCertificateRevocationOptions()),
            CreateLooseMock<ILogger<RevocationRetrievalCache>>().Object
        );

    private static bool IsUnavailableKey(object key) =>
        key is string text && text.EndsWith(":unavailable", StringComparison.Ordinal);

    private static DateTimeOffset InAnHour() => DateTimeOffset.UtcNow.AddHours(1);

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WhenTheRetrievalYieldsAValue_ReusesItUntilItsTime()
    {
        var calls = 0;
        var cache = CreateCache();
        Task<RevocationRetrieval<string>> Retrieve()
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new RevocationRetrieval<string>(Value, InAnHour()));
        }

        var first = await cache.GetAsync<string>(
            Key,
            Location,
            Subject,
            Retrieve,
            CancellationToken.None
        );
        var second = await cache.GetAsync<string>(
            Key,
            Location,
            Subject,
            Retrieve,
            CancellationToken.None
        );

        Assert.Equal(Value, first);
        Assert.Equal(Value, second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetAsync_WhenTheRetrievalHasNoCacheTime_RetrievesAgain()
    {
        var calls = 0;
        var cache = CreateCache();
        Task<RevocationRetrieval<string>> Retrieve()
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new RevocationRetrieval<string>(Value, null));
        }

        var first = await cache.GetAsync<string>(
            Key,
            Location,
            Subject,
            Retrieve,
            CancellationToken.None
        );
        var second = await cache.GetAsync<string>(
            Key,
            Location,
            Subject,
            Retrieve,
            CancellationToken.None
        );

        Assert.Equal(Value, first);
        Assert.Equal(Value, second);
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task GetAsync_WhenTheRetrievalYieldsNothing_RemembersTheFailure()
    {
        var calls = 0;
        var cache = CreateCache();
        Task<RevocationRetrieval<string>> Retrieve()
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(default(RevocationRetrieval<string>));
        }

        var first = await cache.GetAsync<string>(
            Key,
            Location,
            Subject,
            Retrieve,
            CancellationToken.None
        );
        var second = await cache.GetAsync<string>(
            Key,
            Location,
            Subject,
            Retrieve,
            CancellationToken.None
        );

        Assert.Null(first);
        Assert.Null(second);
        Assert.True(cache.IsUnavailable(Key));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetAsync_WhenTwoCallersAskAtOnce_RetrievesOnce()
    {
        var calls = 0;
        var cache = CreateCache();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<RevocationRetrieval<string>> Retrieve()
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return new RevocationRetrieval<string>(Value, InAnHour());
        }

        var first = cache
            .GetAsync<string>(Key, Location, Subject, Retrieve, CancellationToken.None)
            .AsTask();
        var second = cache
            .GetAsync<string>(Key, Location, Subject, Retrieve, CancellationToken.None)
            .AsTask();
        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.Equal(Value, result));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetAsync_WhenTheFirstCallerGivesUp_CompletesTheRetrievalForTheOthers()
    {
        var calls = 0;
        var cache = CreateCache();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task<RevocationRetrieval<string>> Retrieve()
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
            return new RevocationRetrieval<string>(Value, InAnHour());
        }
        using var cancellation = new CancellationTokenSource();

        var first = cache
            .GetAsync<string>(Key, Location, Subject, Retrieve, cancellation.Token)
            .AsTask();
        await cancellation.CancelAsync();
        var second = cache
            .GetAsync<string>(Key, Location, Subject, Retrieve, CancellationToken.None)
            .AsTask();
        gate.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(Value, await second);
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task GetAsync_WhenAFailureIsRememberedBeforeTheRetrievalStarts_DoesNotRetrieve()
    {
        var calls = 0;
        var unavailableChecks = 0;
        object? notCached = null;
        object? remembered = true;
        var mockMemoryCache = CreateStrictMock<IMemoryCache>();
        mockMemoryCache
            .Setup(x => x.TryGetValue(It.Is<object>(key => !IsUnavailableKey(key)), out notCached))
            .Returns(false)
            .Verifiable();
        // The caller's check misses; a retrieval that ends before this one starts has remembered its failure by then.
        mockMemoryCache
            .Setup(x => x.TryGetValue(It.Is<object>(key => IsUnavailableKey(key)), out remembered))
            .Returns(() => Interlocked.Increment(ref unavailableChecks) > 1)
            .Verifiable();
        var cache = CreateCacheOver(mockMemoryCache.Object);
        Task<RevocationRetrieval<string>> Retrieve()
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new RevocationRetrieval<string>(Value, InAnHour()));
        }

        var result = await cache.GetAsync<string>(
            Key,
            Location,
            Subject,
            Retrieve,
            CancellationToken.None
        );

        Assert.Null(result);
        Assert.Equal(0, calls);
        Assert.Equal(2, unavailableChecks);
    }

    [Fact]
    public async Task GetAsync_WhenAValueIsCachedBeforeTheRetrievalStarts_ReturnsItWithoutRetrieving()
    {
        var calls = 0;
        var valueChecks = 0;
        object? cachedValue = Value;
        object? notRemembered = null;
        var mockMemoryCache = CreateStrictMock<IMemoryCache>();
        // The caller's check misses; a retrieval that ends before this one starts has cached its value by then.
        mockMemoryCache
            .Setup(x =>
                x.TryGetValue(It.Is<object>(key => !IsUnavailableKey(key)), out cachedValue)
            )
            .Returns(() => Interlocked.Increment(ref valueChecks) > 1)
            .Verifiable();
        mockMemoryCache
            .Setup(x =>
                x.TryGetValue(It.Is<object>(key => IsUnavailableKey(key)), out notRemembered)
            )
            .Returns(false)
            .Verifiable();
        var cache = CreateCacheOver(mockMemoryCache.Object);
        Task<RevocationRetrieval<string>> Retrieve()
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new RevocationRetrieval<string>(Value, InAnHour()));
        }

        var result = await cache.GetAsync<string>(
            Key,
            Location,
            Subject,
            Retrieve,
            CancellationToken.None
        );

        Assert.Equal(Value, result);
        Assert.Equal(0, calls);
        Assert.Equal(2, valueChecks);
    }

    #endregion

    #region MarkUnavailable Tests

    [Fact]
    public async Task MarkUnavailable_MakesTheKeyUnavailable()
    {
        var calls = 0;
        var cache = CreateCache();
        Task<RevocationRetrieval<string>> Retrieve()
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new RevocationRetrieval<string>(Value, InAnHour()));
        }
        Assert.False(cache.IsUnavailable(Key));

        cache.MarkUnavailable(Key);
        var result = await cache.GetAsync<string>(
            Key,
            Location,
            Subject,
            Retrieve,
            CancellationToken.None
        );

        Assert.True(cache.IsUnavailable(Key));
        Assert.Null(result);
        Assert.Equal(0, calls);
    }

    #endregion
}
