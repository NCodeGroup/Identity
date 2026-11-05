// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Linq.Expressions;
using Moq;
using NCode.Identity.Persistence;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests;

public class PostCommitWorkTests : BaseTests
{
    #region Scaffold

    private const string State = "acme";

    private static Expression<Func<TimeProvider, ITimer>> CreateTimerExpression =>
        x =>
            x.CreateTimer(
                It.IsAny<TimerCallback>(),
                It.IsAny<object?>(),
                PostCommitWork.Timeout,
                Timeout.InfiniteTimeSpan
            );

    private Mock<TimeProvider> CreateSystemTimeProvider()
    {
        var mockTimeProvider = CreateStrictMock<TimeProvider>();
        mockTimeProvider
            .Setup(CreateTimerExpression)
            .Returns(
                (TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
                    TimeProvider.System.CreateTimer(callback, state, dueTime, period)
            )
            .Verifiable();
        return mockTimeProvider;
    }

    #endregion

    #region RunAsync Tests

    [Fact]
    public async Task RunAsync_RunsTheStepWithTheStateOnATokenThatIsNotCancelled()
    {
        var mockTimeProvider = CreateSystemTimeProvider();
        var receivedState = string.Empty;
        var receivedToken = CancellationToken.None;
        ValueTask Step(string state, CancellationToken cancellationToken)
        {
            receivedState = state;
            receivedToken = cancellationToken;
            return ValueTask.CompletedTask;
        }

        await PostCommitWork.RunAsync(mockTimeProvider.Object, Step, State);

        Assert.Equal(State, receivedState);
        Assert.True(receivedToken.CanBeCanceled);
        Assert.False(receivedToken.IsCancellationRequested);
    }

    [Fact]
    public async Task RunAsync_WhenTheTimeoutElapses_CancelsTheStepsToken()
    {
        TimerCallback? timerCallback = null;
        object? timerState = null;
        var mockTimeProvider = CreateStrictMock<TimeProvider>();
        mockTimeProvider
            .Setup(CreateTimerExpression)
            .Returns(
                (TimerCallback callback, object? state, TimeSpan _, TimeSpan _) =>
                {
                    timerCallback = callback;
                    timerState = state;
                    return TimeProvider.System.CreateTimer(
                        callback,
                        state,
                        Timeout.InfiniteTimeSpan,
                        Timeout.InfiniteTimeSpan
                    );
                }
            )
            .Verifiable();
        var cancelled = false;
        ValueTask Step(string _, CancellationToken cancellationToken)
        {
            Assert.NotNull(timerCallback);
            timerCallback(timerState);
            cancelled = cancellationToken.IsCancellationRequested;
            return ValueTask.CompletedTask;
        }

        await PostCommitWork.RunAsync(mockTimeProvider.Object, Step, State);

        Assert.True(cancelled);
    }

    [Fact]
    public async Task RunAsync_WhenTheStepFails_Throws()
    {
        var mockTimeProvider = CreateSystemTimeProvider();
        Func<string, CancellationToken, ValueTask> step = (_, _) =>
            ValueTask.FromException(new InvalidOperationException("The step failed."));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await PostCommitWork.RunAsync(mockTimeProvider.Object, step, State)
        );

        Assert.Equal("The step failed.", exception.Message);
    }

    #endregion
}
