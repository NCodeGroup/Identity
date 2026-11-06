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
    private static readonly TimeSpan WaitLimit = TimeSpan.FromSeconds(10);

    private readonly BffTestOptionsSource _source;
    private readonly BffFrontendCatalog _frontends;
    private readonly Mock<BffSessionRetirement> _retirement;
    private readonly Channel<BffFrontendSet> _passes = Channel.CreateUnbounded<BffFrontendSet>();
    private readonly FakeTimeProvider _timeProvider = new();
    private readonly FakeLogger<BffSessionRetirementService> _logger = new();

    public BffSessionRetirementServiceTests()
    {
        _source = new BffTestOptionsSource(BffTestFrontends.CreateBffOptions());
        _frontends = BffTestFrontends.CreateCatalog(_source);
        _retirement = CreateStrictMock<BffSessionRetirement>(
            BffTestTelemetry.Instrument(CreateStrictMock<IBffSessionStore>().Object),
            CreateStrictMock<IBffSessionManager>().Object,
            TimeProvider.System,
            NullLogger<BffSessionRetirement>.Instance
        );
    }

    private BffSessionRetirementService CreateService(TimeProvider? timeProvider = null) =>
        new(_frontends, _retirement.Object, timeProvider ?? _timeProvider, _logger);

    private void SetupRetire(
        int calls,
        Func<int, Exception?> failure,
        Func<int, bool>? complete = null
    )
    {
        complete ??= _ => true;
        var made = 0;
        _retirement
            .Setup(x => x.RetireAsync(It.IsAny<BffFrontendSet>(), It.IsAny<CancellationToken>()))
            .Returns(
                (BffFrontendSet set, CancellationToken _) =>
                {
                    var call = Interlocked.Increment(ref made);
                    _passes.Writer.TryWrite(set);
                    return failure(call) is { } exception
                        ? throw exception
                        : ValueTask.FromResult(complete(call));
                }
            )
            .Verifiable(Times.Exactly(calls));
    }

    private static BffStoreUnavailableException CreateStoreFailure() =>
        new("The store is unavailable.");

    private async Task<BffFrontendSet> ReadPassAsync() =>
        await _passes.Reader.ReadAsync().AsTask().WaitAsync(WaitLimit);

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

    private static async Task FireRetryTimerAsync(
        Channel<(TimerCallback Callback, object? State, TimeSpan DueTime)> timers
    )
    {
        while (true)
        {
            var (callback, state, dueTime) = await timers
                .Reader.ReadAsync()
                .AsTask()
                .WaitAsync(WaitLimit);
            if (dueTime != BffSessionRetirementService.RetryDelay)
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
        var set = await ReadPassAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Same(_frontends.Current, set);
    }

    [Fact]
    public async Task ExecuteAsync_AfterAChange_RetiresAgainstTheChangedSet()
    {
        SetupRetire(2, _ => null);
        using var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        var first = await ReadPassAsync();
        _source.Change(
            BffTestFrontends.CreateBffOptions(options =>
                options.Frontends["other"] = BffTestFrontends.CreateOptions(frontend =>
                {
                    frontend.ClientId = "other";
                    frontend.PublicOrigin = "https://other.example";
                })
            )
        );
        var second = await ReadPassAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.NotSame(first, second);
        Assert.Same(_frontends.Current, second);
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
        await ReadPassAsync();
        await FireRetryTimerAsync(timers);
        await ReadPassAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Contains(
            _logger.Collector.GetSnapshot(),
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
        await ReadPassAsync();
        await FireRetryTimerAsync(timers);
        await ReadPassAsync();
        await service.StopAsync(CancellationToken.None);

        Assert.Contains(
            _logger.Collector.GetSnapshot(),
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
        await ReadPassAsync();
        await FireRetryTimerAsync(timers);
        await ReadPassAsync();
        await service.StopAsync(CancellationToken.None);
    }

    #endregion
}
