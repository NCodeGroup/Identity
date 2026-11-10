// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Channels;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Hosting;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Diagnostics;
using NCode.Identity.Bff.Tests.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Hosting;

public sealed class BffSessionRetirementServiceTests : BaseTests
{
    private static TimeSpan WaitLimit { get; } = TimeSpan.FromSeconds(10);
    private static TimeSpan RetryDelay { get; } = TimeSpan.FromMinutes(2);

    private BffTestOptionsSource Source { get; }
    private BffFrontendCatalog Frontends { get; }
    private Mock<BffSessionRetirement> Retirement { get; }
    private Channel<BffFrontendSet> Passes { get; } = Channel.CreateUnbounded<BffFrontendSet>();
    private FakeTimeProvider Clock { get; } = new();
    private FakeLogger<BffSessionRetirementService> Logger { get; } = new();

    public BffSessionRetirementServiceTests()
    {
        Source = new BffTestOptionsSource(
            BffTestFrontends.CreateBffOptions(options =>
                options.SessionRetirement.RetryDelay = RetryDelay
            )
        );
        Frontends = BffTestFrontends.CreateCatalog(Source);
        Retirement = CreateStrictMock<BffSessionRetirement>(
            BffTestTelemetry.Instrument(CreateStrictMock<IBffSessionStore>().Object),
            CreateStrictMock<IBffSessionManager>().Object,
            TimeProvider.System,
            NullLogger<BffSessionRetirement>.Instance
        );
    }

    private BffSessionRetirementService CreateService(TimeProvider? timeProvider = null) =>
        new(Frontends, Retirement.Object, timeProvider ?? Clock, Logger);

    private void SetupRetire(
        int calls,
        Func<int, Exception?> failure,
        Func<int, bool>? complete = null
    )
    {
        complete ??= _ => true;
        var made = 0;
        Retirement
            .Setup(x => x.RetireAsync(It.IsAny<BffFrontendSet>(), It.IsAny<CancellationToken>()))
            .Returns(
                (BffFrontendSet set, CancellationToken _) =>
                {
                    var call = Interlocked.Increment(ref made);
                    Passes.Writer.TryWrite(set);
                    return failure(call) is { } exception
                        ? throw exception
                        : ValueTask.FromResult(complete(call));
                }
            )
            .Verifiable(Times.Exactly(calls));
    }

    private static BffStoreUnavailableException CreateStoreFailure() =>
        new("The store is unavailable.");

    private async ValueTask<BffFrontendSet> ReadPassAsync(CancellationToken cancellationToken) =>
        await Passes
            .Reader.ReadAsync(cancellationToken)
            .AsTask()
            .WaitAsync(WaitLimit, cancellationToken);

    private Mock<TimeProvider> CreateTimerProvider(
        Channel<(TimerCallback Callback, object? State, TimeSpan DueTime)> timers
    )
    {
        var mockTimer = CreateStrictMock<ITimer>();
        mockTimer.Setup(x => x.Dispose()).Verifiable();
        var mockTimeProvider = CreateStrictMock<TimeProvider>();
        mockTimeProvider
            .Setup(x =>
                x.CreateTimer(
                    It.IsAny<TimerCallback>(),
                    It.IsAny<object?>(),
                    It.IsAny<TimeSpan>(),
                    Timeout.InfiniteTimeSpan
                )
            )
            .Returns(
                (TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan _) =>
                {
                    timers.Writer.TryWrite((callback, state, dueTime));
                    return mockTimer.Object;
                }
            )
            .Verifiable();
        return mockTimeProvider;
    }

    private static async ValueTask FireRetryTimerAsync(
        Channel<(TimerCallback Callback, object? State, TimeSpan DueTime)> timers,
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            var (callback, state, dueTime) = await timers
                .Reader.ReadAsync(cancellationToken)
                .AsTask()
                .WaitAsync(WaitLimit, cancellationToken);
            if (dueTime != RetryDelay)
                continue;

            callback(state);
            return;
        }
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_Start_RetiresAgainstTheCurrentSet()
    {
        SetupRetire(1, _ => null);
        using var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        var set = await ReadPassAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Same(Frontends.Current, set);
    }

    [Fact]
    public async Task ExecuteAsync_AfterAChange_RetiresAgainstTheChangedSet()
    {
        SetupRetire(2, _ => null);
        using var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        var first = await ReadPassAsync(CancellationToken.None);
        Source.Change(
            BffTestFrontends.CreateBffOptions(options =>
                options.Frontends["other"] = BffTestFrontends.CreateOptions(frontend =>
                {
                    frontend.ClientId = "other";
                    frontend.PublicOrigin = "https://other.example";
                })
            )
        );
        var second = await ReadPassAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.Same(Frontends.Current, second);
        Assert.Contains(second.Frontends, frontend => frontend.Name == "other");
    }

    [Fact]
    public async Task ExecuteAsync_StoreFails_RetriesAfterTheDelay()
    {
        SetupRetire(2, call => call == 1 ? CreateStoreFailure() : null);
        var timers = Channel.CreateUnbounded<(
            TimerCallback Callback,
            object? State,
            TimeSpan DueTime
        )>();
        var timeProvider = CreateTimerProvider(timers);
        using var service = CreateService(timeProvider.Object);

        await service.StartAsync(CancellationToken.None);
        await ReadPassAsync(CancellationToken.None);
        await FireRetryTimerAsync(timers, CancellationToken.None);
        await ReadPassAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Contains(
            Logger.Collector.GetSnapshot(),
            record =>
                record.Id.Id == EventIds.FrontendRetirementFailed
                && record.Exception is BffStoreUnavailableException
        );
    }

    [Fact]
    public async Task ExecuteAsync_PassThrowsUnexpectedException_RetriesAfterTheDelay()
    {
        SetupRetire(
            2,
            call => call == 1 ? new InvalidOperationException("The pass failed.") : null
        );
        var timers = Channel.CreateUnbounded<(
            TimerCallback Callback,
            object? State,
            TimeSpan DueTime
        )>();
        var timeProvider = CreateTimerProvider(timers);
        using var service = CreateService(timeProvider.Object);

        await service.StartAsync(CancellationToken.None);
        await ReadPassAsync(CancellationToken.None);
        await FireRetryTimerAsync(timers, CancellationToken.None);
        await ReadPassAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        Assert.Contains(
            Logger.Collector.GetSnapshot(),
            record =>
                record.Id.Id == EventIds.FrontendRetirementFailed
                && record.Exception is InvalidOperationException
        );
    }

    [Fact]
    public async Task ExecuteAsync_LeaseLeftToAnotherHolder_RetriesAfterTheDelay()
    {
        SetupRetire(2, _ => null, call => call != 1);
        var timers = Channel.CreateUnbounded<(
            TimerCallback Callback,
            object? State,
            TimeSpan DueTime
        )>();
        var timeProvider = CreateTimerProvider(timers);
        using var service = CreateService(timeProvider.Object);

        await service.StartAsync(CancellationToken.None);
        await ReadPassAsync(CancellationToken.None);
        await FireRetryTimerAsync(timers, CancellationToken.None);
        await ReadPassAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);
    }

    #endregion
}
