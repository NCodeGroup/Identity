// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.Bff.Logging;
using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public sealed class DefaultBffConnectionRevalidatorTests : BaseTests
{
    private static DateTimeOffset Start { get; } = new(2023, 11, 14, 22, 13, 20, TimeSpan.Zero);

    private static TimeSpan Interval { get; } = TimeSpan.FromMinutes(1);

    private static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(10);

    private Mock<IBffConnectionSession> MockSession { get; }
    private Mock<TimeProvider> MockTimeProvider { get; }
    private FakeTimeProvider TimeProvider { get; }
    private FakeLogger<DefaultBffConnectionRevalidator> Logger { get; }
    private DefaultBffConnectionRevalidator Revalidator { get; }
    private TaskCompletionSource Armed { get; set; }
    private CancellationTokenSource EndedSource { get; } = new();

    public DefaultBffConnectionRevalidatorTests()
    {
        MockSession = CreateStrictMock<IBffConnectionSession>();
        MockSession.SetupGet(x => x.SessionEnded).Returns(EndedSource.Token).Verifiable();
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        TimeProvider = new FakeTimeProvider(Start);
        Logger = new FakeLogger<DefaultBffConnectionRevalidator>();
        Revalidator = new DefaultBffConnectionRevalidator(MockTimeProvider.Object, Logger);
        Armed = CreateSignal();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            EndedSource.Dispose();

        base.Dispose(disposing);
    }

    /// <summary>
    /// Forwards every delay the revalidator arms to the fake clock and signals <see cref="Armed"/> once the delay's
    /// timer exists, so a test advances the clock only after the revalidator waits for it.
    /// </summary>
    private void SetupArmedDelays() =>
        MockTimeProvider
            .Setup(x =>
                x.CreateTimer(
                    It.IsAny<TimerCallback>(),
                    It.IsAny<object?>(),
                    It.IsAny<TimeSpan>(),
                    It.IsAny<TimeSpan>()
                )
            )
            .Returns(
                (TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
                {
                    var timer = TimeProvider.CreateTimer(callback, state, dueTime, period);
                    Armed.TrySetResult();
                    return timer;
                }
            )
            .Verifiable();

    private async ValueTask AdvanceOnceAsync(CancellationToken cancellationToken)
    {
        await Armed.Task.WaitAsync(Timeout, cancellationToken);
        Armed = CreateSignal();
        TimeProvider.Advance(Interval);
    }

    private static TaskCompletionSource CreateSignal() =>
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static ClaimsPrincipal CreatePrincipal() =>
        new(new ClaimsIdentity([new Claim("sub", "user-1")], "test"));

    #region WaitUntilEndedAsync Tests

    [Fact]
    public async Task WaitUntilEndedAsync_SessionEnded_Completes()
    {
        MockSession.Setup(x => x.RevalidationInterval).Returns(Interval).Verifiable();
        MockSession
            .Setup(x => x.ValidateAsync(null, It.IsAny<CancellationToken>()))
            .ReturnsAsync((ClaimsPrincipal?)null)
            .Verifiable();
        SetupArmedDelays();

        var task = Revalidator
            .WaitUntilEndedAsync(MockSession.Object, CancellationToken.None)
            .AsTask();

        await AdvanceOnceAsync(CancellationToken.None);

        await task.WaitAsync(Timeout);
        Assert.Empty(Logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task WaitUntilEndedAsync_SessionAlive_KeepsWaiting()
    {
        var calls = 0;
        var validated = CreateSignal();
        var principal = CreatePrincipal();
        MockSession.Setup(x => x.RevalidationInterval).Returns(Interval).Verifiable();
        MockSession
            .Setup(x => x.ValidateAsync(null, It.IsAny<CancellationToken>()))
            .Callback(() =>
            {
                if (Interlocked.Increment(ref calls) == 2)
                    validated.TrySetResult();
            })
            .ReturnsAsync(principal)
            .Verifiable();
        SetupArmedDelays();
        using var cancellation = new CancellationTokenSource();
        var task = Revalidator.WaitUntilEndedAsync(MockSession.Object, cancellation.Token).AsTask();

        await AdvanceOnceAsync(CancellationToken.None);
        await AdvanceOnceAsync(CancellationToken.None);
        await validated.Task.WaitAsync(Timeout);

        Assert.False(task.IsCompleted);
        Assert.Empty(Logger.Collector.GetSnapshot());
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task WaitUntilEndedAsync_StoreUnavailable_LogsAndKeepsWaiting()
    {
        var calls = 0;
        var validated = CreateSignal();
        var principal = CreatePrincipal();
        MockSession.Setup(x => x.RevalidationInterval).Returns(Interval).Verifiable();
        MockSession
            .Setup(x => x.ValidateAsync(null, It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                if (Interlocked.Increment(ref calls) == 1)
                    throw new BffStoreUnavailableException("store down");

                validated.TrySetResult();
                return ValueTask.FromResult<ClaimsPrincipal?>(principal);
            })
            .Verifiable();
        SetupArmedDelays();
        using var cancellation = new CancellationTokenSource();
        var task = Revalidator.WaitUntilEndedAsync(MockSession.Object, cancellation.Token).AsTask();

        await AdvanceOnceAsync(CancellationToken.None);
        await AdvanceOnceAsync(CancellationToken.None);
        await validated.Task.WaitAsync(Timeout);

        Assert.False(task.IsCompleted);
        var record = Assert.Single(Logger.Collector.GetSnapshot());
        Assert.Equal(EventIds.ConnectionRevalidationFailed, record.Id.Id);
        Assert.IsType<BffStoreUnavailableException>(record.Exception);
        await cancellation.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task WaitUntilEndedAsync_ValidationThrows_LogsAndCompletes()
    {
        MockSession.Setup(x => x.RevalidationInterval).Returns(Interval).Verifiable();
        MockSession
            .Setup(x => x.ValidateAsync(null, It.IsAny<CancellationToken>()))
            .Returns(() => throw new InvalidOperationException("validation failed"))
            .Verifiable();
        SetupArmedDelays();

        var task = Revalidator
            .WaitUntilEndedAsync(MockSession.Object, CancellationToken.None)
            .AsTask();

        await AdvanceOnceAsync(CancellationToken.None);

        await task.WaitAsync(Timeout);
        var record = Assert.Single(Logger.Collector.GetSnapshot());
        Assert.Equal(EventIds.ConnectionRevalidationFaulted, record.Id.Id);
        Assert.IsType<InvalidOperationException>(record.Exception);
    }

    [Fact]
    public async Task WaitUntilEndedAsync_Cancelled_Throws()
    {
        MockSession.Setup(x => x.RevalidationInterval).Returns(Interval).Verifiable();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var task = Revalidator.WaitUntilEndedAsync(MockSession.Object, cancellation.Token).AsTask();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        Assert.Empty(Logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task WaitUntilEndedAsync_SessionEndedSignaled_CompletesWithoutValidating()
    {
        MockSession.Setup(x => x.RevalidationInterval).Returns(Interval).Verifiable();
        SetupArmedDelays();
        var task = Revalidator
            .WaitUntilEndedAsync(MockSession.Object, CancellationToken.None)
            .AsTask();

        await Armed.Task.WaitAsync(Timeout);
        await EndedSource.CancelAsync();

        await task.WaitAsync(Timeout);
        Assert.Empty(Logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task WaitUntilEndedAsync_SessionAlreadyEnded_CompletesAtOnce()
    {
        await EndedSource.CancelAsync();

        var task = Revalidator
            .WaitUntilEndedAsync(MockSession.Object, CancellationToken.None)
            .AsTask();

        Assert.True(task.IsCompletedSuccessfully);
    }

    #endregion
}
