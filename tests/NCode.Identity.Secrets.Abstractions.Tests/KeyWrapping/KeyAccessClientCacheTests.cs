// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Time.Testing;

namespace NCode.Identity.Secrets.KeyWrapping;

public class KeyAccessClientCacheTests
{
    private static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(10);

    private FakeTimeProvider TimeProvider { get; } = new();

    private int CreateCount { get; set; }

    private List<FakeDisposableClient> Clients { get; } = [];

    private KeyAccessClientCache<FakeDisposableClient> CreateCache() =>
        new(TimeProvider, IdleTimeout);

    private ValueTask<FakeDisposableClient> CreateClient(CancellationToken cancellationToken)
    {
        CreateCount++;
        var client = new FakeDisposableClient();
        Clients.Add(client);
        return ValueTask.FromResult(client);
    }

    private static ValueTask<int> Succeed(
        FakeDisposableClient client,
        CancellationToken cancellationToken
    ) => ValueTask.FromResult(1);

    private static ValueTask<int> Fail(
        FakeDisposableClient client,
        CancellationToken cancellationToken
    ) => throw new InvalidOperationException("The operation failed.");

    private static ValueTask<int> Cancel(
        FakeDisposableClient client,
        CancellationToken cancellationToken
    ) => throw new OperationCanceledException();

    #region UseAsync Tests

    [Fact]
    public async Task UseAsync_WhenFirstUsesAreConcurrent_CreatesTheClientOnce()
    {
        using var cache = CreateCache();
        var gate = new TaskCompletionSource<FakeDisposableClient>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var createCount = 0;

        async ValueTask<FakeDisposableClient> CreateGated(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref createCount);
            return await gate.Task;
        }

        var uses = Enumerable
            .Range(0, 5)
            .Select(_ =>
                cache.UseAsync("key", CreateGated, Succeed, CancellationToken.None).AsTask()
            )
            .ToArray();

        gate.SetResult(new FakeDisposableClient());
        var results = await Task.WhenAll(uses);

        Assert.Equal(1, createCount);
        Assert.All(results, result => Assert.Equal(1, result));
    }

    [Fact]
    public async Task UseAsync_WhenTheKeyIsCached_ReusesTheClient()
    {
        using var cache = CreateCache();
        var seen = new List<FakeDisposableClient>();

        ValueTask<int> Record(FakeDisposableClient client, CancellationToken cancellationToken)
        {
            seen.Add(client);
            return ValueTask.FromResult(1);
        }

        await cache.UseAsync("key", CreateClient, Record, CancellationToken.None);
        await cache.UseAsync("key", CreateClient, Record, CancellationToken.None);

        Assert.Equal(1, CreateCount);
        Assert.Same(seen[0], seen[1]);
    }

    [Fact]
    public async Task UseAsync_WhenAConfirmedEntryFails_KeepsTheEntry()
    {
        using var cache = CreateCache();
        await cache.UseAsync("key", CreateClient, Succeed, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await cache.UseAsync("key", CreateClient, Fail, CancellationToken.None)
        );
        await cache.UseAsync("key", CreateClient, Succeed, CancellationToken.None);

        Assert.Equal(1, CreateCount);
        Assert.Equal(0, Clients[0].DisposeCount);
    }

    [Fact]
    public async Task UseAsync_WhenAnUnconfirmedEntryFails_RemovesTheEntryAndDisposesTheClient()
    {
        using var cache = CreateCache();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await cache.UseAsync("key", CreateClient, Fail, CancellationToken.None)
        );
        await cache.UseAsync("key", CreateClient, Succeed, CancellationToken.None);

        Assert.Equal(2, CreateCount);
        Assert.Equal(1, Clients[0].DisposeCount);
        Assert.Equal(0, Clients[1].DisposeCount);
    }

    [Fact]
    public async Task UseAsync_WhenAnEntryIsReleasedWithAUseInFlight_DisposesTheClientWhenThatUseExits()
    {
        using var cache = CreateCache();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var proceed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        async ValueTask<int> Hold(FakeDisposableClient client, CancellationToken cancellationToken)
        {
            entered.SetResult();
            await proceed.Task;
            return 1;
        }

        var held = cache.UseAsync("key", CreateClient, Hold, CancellationToken.None).AsTask();
        await entered.Task;

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await cache.UseAsync("key", CreateClient, Fail, CancellationToken.None)
        );

        Assert.Equal(0, Clients[0].DisposeCount);

        proceed.SetResult();
        await held;

        Assert.Equal(1, CreateCount);
        Assert.Equal(1, Clients[0].DisposeCount);
    }

    [Fact]
    public async Task UseAsync_WhenCreationFails_DoesNotCacheTheFailure()
    {
        using var cache = CreateCache();
        var attempts = 0;

        ValueTask<FakeDisposableClient> CreateOnceFailing(CancellationToken cancellationToken)
        {
            attempts++;
            if (attempts == 1)
                throw new InvalidOperationException("The client cannot be created.");

            return CreateClient(cancellationToken);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await cache.UseAsync("key", CreateOnceFailing, Succeed, CancellationToken.None)
        );
        var result = await cache.UseAsync(
            "key",
            CreateOnceFailing,
            Succeed,
            CancellationToken.None
        );

        Assert.Equal(1, result);
        Assert.Equal(2, attempts);
        Assert.Single(Clients);
        Assert.Equal(0, Clients[0].DisposeCount);
    }

    [Fact]
    public async Task UseAsync_WhenTheOperationIsCanceled_ReleasesNothing()
    {
        using var cache = CreateCache();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await cache.UseAsync("key", CreateClient, Cancel, CancellationToken.None)
        );
        await cache.UseAsync("key", CreateClient, Succeed, CancellationToken.None);

        Assert.Equal(1, CreateCount);
        Assert.Equal(0, Clients[0].DisposeCount);
    }

    [Fact]
    public async Task UseAsync_WhenAnEntryIsIdleLongerThanTheTimeout_DisposesItsClient()
    {
        using var cache = CreateCache();
        await cache.UseAsync("idle", CreateClient, Succeed, CancellationToken.None);

        TimeProvider.Advance(IdleTimeout + TimeSpan.FromSeconds(1));
        await cache.UseAsync("other", CreateClient, Succeed, CancellationToken.None);

        Assert.Equal(1, Clients[0].DisposeCount);
        Assert.Equal(0, Clients[1].DisposeCount);

        await cache.UseAsync("idle", CreateClient, Succeed, CancellationToken.None);

        Assert.Equal(3, CreateCount);
    }

    [Fact]
    public async Task UseAsync_WhenAnEntryIsIdleForTheTimeout_KeepsItsClient()
    {
        using var cache = CreateCache();
        await cache.UseAsync("key", CreateClient, Succeed, CancellationToken.None);

        TimeProvider.Advance(IdleTimeout);
        await cache.UseAsync("key", CreateClient, Succeed, CancellationToken.None);

        Assert.Equal(1, CreateCount);
        Assert.Equal(0, Clients[0].DisposeCount);
    }

    [Fact]
    public async Task UseAsync_WhenTheClientIsNotDisposable_RunsTheOperation()
    {
        using var cache = new KeyAccessClientCache<string>(TimeProvider, IdleTimeout);

        var result = await cache.UseAsync(
            "key",
            _ => ValueTask.FromResult("client"),
            (client, _) => ValueTask.FromResult(client.Length),
            CancellationToken.None
        );
        Assert.Equal(6, result);

        TimeProvider.Advance(IdleTimeout + TimeSpan.FromSeconds(1));
        result = await cache.UseAsync(
            "other",
            _ => ValueTask.FromResult("other"),
            (client, _) => ValueTask.FromResult(client.Length),
            CancellationToken.None
        );

        Assert.Equal(5, result);
    }

    #endregion

    #region Dispose Tests

    [Fact]
    public async Task Dispose_WhenClientsAreCached_DisposesEveryClient()
    {
        var cache = CreateCache();
        await cache.UseAsync("one", CreateClient, Succeed, CancellationToken.None);
        await cache.UseAsync("two", CreateClient, Succeed, CancellationToken.None);

        cache.Dispose();

        Assert.All(Clients, client => Assert.Equal(1, client.DisposeCount));
        Assert.Equal(2, Clients.Count);
    }

    [Fact]
    public async Task Dispose_WhenCalledTwice_DisposesEachClientOnce()
    {
        var cache = CreateCache();
        await cache.UseAsync("key", CreateClient, Succeed, CancellationToken.None);

        cache.Dispose();
        cache.Dispose();

        Assert.Equal(1, Clients[0].DisposeCount);
    }

    #endregion
}
