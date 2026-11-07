// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Authentication.Subject;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Persistence;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Subject;

public sealed class DefaultPostCommitSecurityEffectsTests : BaseTests
{
    private const string TenantId = "tenant-1";

    private const string SubjectId = "principal-1";

    private const string SessionId = "session-1";

    private const string UserName = "alice";

    private static DateTimeOffset Now { get; } = new(2026, 11, 6, 12, 0, 0, TimeSpan.Zero);

    private Mock<ISessionService> MockSessionService { get; }

    private Mock<IPasswordAttemptLimiter> MockPasswordAttemptLimiter { get; }

    private FakeTimeProvider TimeProvider { get; } = new(Now);

    private FakeLogger<DefaultPostCommitSecurityEffects> Logger { get; } = new();

    private DefaultPostCommitSecurityEffects Effects { get; }

    public DefaultPostCommitSecurityEffectsTests()
    {
        MockSessionService = CreateStrictMock<ISessionService>();
        MockPasswordAttemptLimiter = CreateStrictMock<IPasswordAttemptLimiter>();
        Effects = new DefaultPostCommitSecurityEffects(
            MockSessionService.Object,
            MockPasswordAttemptLimiter.Object,
            TimeProvider,
            Logger
        );
    }

    #region Scaffolding

    private OpenIdContext CreateContext()
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();

        return mockContext.Object;
    }

    private OpenIdContext CreateContextWithoutSetups()
    {
        var mockContext = CreateStrictMock<OpenIdContext>();
        return mockContext.Object;
    }

    #endregion

    #region TerminateSessionsAsync Tests

    [Fact]
    public async Task TerminateSessionsAsync_Always_RevokesEverySessionOnABoundedToken()
    {
        var context = CreateContextWithoutSetups();
        MockSessionService
            .Setup(x =>
                x.RevokeAllAsync(
                    context,
                    SubjectId,
                    SessionTerminationReasons.UserBlocked,
                    It.Is<CancellationToken>(x => x.CanBeCanceled && !x.IsCancellationRequested)
                )
            )
            .ReturnsAsync(1)
            .Verifiable();

        await Effects.TerminateSessionsAsync(
            context,
            SubjectId,
            SessionTerminationReasons.UserBlocked
        );

        var records = Logger.Collector.GetSnapshot();
        Assert.Empty(records);
    }

    [Fact]
    public async Task TerminateSessionsAsync_WhenTheBoundElapses_LogsTheFailureAndReturns()
    {
        var context = CreateContext();
        MockSessionService
            .Setup(x =>
                x.RevokeAllAsync(
                    context,
                    SubjectId,
                    SessionTerminationReasons.UserBlocked,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (OpenIdContext _, string _, string _, CancellationToken x) =>
                {
                    TimeProvider.Advance(PostCommitWork.Timeout);
                    return x.IsCancellationRequested
                        ? ValueTask.FromCanceled<int>(x)
                        : ValueTask.FromResult(0);
                }
            )
            .Verifiable();

        await Effects.TerminateSessionsAsync(
            context,
            SubjectId,
            SessionTerminationReasons.UserBlocked
        );

        var snapshot = Logger.Collector.GetSnapshot();
        var record = Assert.Single(snapshot);
        Assert.Equal(EventIds.PostCommitSessionTerminationFailed, record.Id.Id);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.IsAssignableFrom<OperationCanceledException>(record.Exception);
    }

    [Fact]
    public async Task TerminateSessionsAsync_WhenTheTerminationFails_LogsTheFailureAndReturns()
    {
        var context = CreateContext();
        MockSessionService
            .Setup(x =>
                x.RevokeAllAsync(
                    context,
                    SubjectId,
                    SessionTerminationReasons.UserBlocked,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException())
            .Verifiable();

        await Effects.TerminateSessionsAsync(
            context,
            SubjectId,
            SessionTerminationReasons.UserBlocked
        );

        var snapshot = Logger.Collector.GetSnapshot();
        var record = Assert.Single(snapshot);
        Assert.Equal(EventIds.PostCommitSessionTerminationFailed, record.Id.Id);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.IsAssignableFrom<InvalidOperationException>(record.Exception);
    }

    #endregion

    #region TerminateOtherSessionsAsync Tests

    [Fact]
    public async Task TerminateOtherSessionsAsync_Always_RevokesTheOtherSessionsOnABoundedToken()
    {
        var context = CreateContextWithoutSetups();
        MockSessionService
            .Setup(x =>
                x.RevokeAllExceptAsync(
                    context,
                    SubjectId,
                    SessionId,
                    SessionTerminationReasons.PasswordChanged,
                    It.Is<CancellationToken>(x => x.CanBeCanceled && !x.IsCancellationRequested)
                )
            )
            .ReturnsAsync(1)
            .Verifiable();

        await Effects.TerminateOtherSessionsAsync(
            context,
            SubjectId,
            SessionId,
            SessionTerminationReasons.PasswordChanged
        );

        var records = Logger.Collector.GetSnapshot();
        Assert.Empty(records);
    }

    [Fact]
    public async Task TerminateOtherSessionsAsync_WhenTheTerminationFails_LogsTheFailureAndReturns()
    {
        var context = CreateContext();
        MockSessionService
            .Setup(x =>
                x.RevokeAllExceptAsync(
                    context,
                    SubjectId,
                    SessionId,
                    SessionTerminationReasons.PasswordChanged,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(new InvalidOperationException())
            .Verifiable();

        await Effects.TerminateOtherSessionsAsync(
            context,
            SubjectId,
            SessionId,
            SessionTerminationReasons.PasswordChanged
        );

        var snapshot = Logger.Collector.GetSnapshot();
        var record = Assert.Single(snapshot);
        Assert.Equal(EventIds.PostCommitSessionTerminationFailed, record.Id.Id);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.IsAssignableFrom<InvalidOperationException>(record.Exception);
    }

    [Fact]
    public async Task TerminateOtherSessionsAsync_WhenTheBoundElapses_LogsAnErrorAndDoesNotThrow()
    {
        var context = CreateContext();
        MockSessionService
            .Setup(x =>
                x.RevokeAllExceptAsync(
                    context,
                    SubjectId,
                    SessionId,
                    SessionTerminationReasons.PasswordChanged,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(
                (OpenIdContext _, string _, string _, string _, CancellationToken x) =>
                {
                    TimeProvider.Advance(PostCommitWork.Timeout);
                    return x.IsCancellationRequested
                        ? ValueTask.FromCanceled<int>(x)
                        : ValueTask.FromResult(0);
                }
            )
            .Verifiable();

        await Effects.TerminateOtherSessionsAsync(
            context,
            SubjectId,
            SessionId,
            SessionTerminationReasons.PasswordChanged
        );

        var snapshot = Logger.Collector.GetSnapshot();
        var record = Assert.Single(snapshot);
        Assert.Equal(EventIds.PostCommitSessionTerminationFailed, record.Id.Id);
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.IsAssignableFrom<OperationCanceledException>(record.Exception);
    }

    #endregion

    #region ResetPasswordAttemptsAsync Tests

    [Fact]
    public async Task ResetPasswordAttemptsAsync_Always_ClearsTheCountOnABoundedToken()
    {
        MockPasswordAttemptLimiter
            .Setup(x =>
                x.ResetAsync(
                    TenantId,
                    UserName,
                    It.Is<CancellationToken>(x => x.CanBeCanceled && !x.IsCancellationRequested)
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Effects.ResetPasswordAttemptsAsync(TenantId, UserName);

        var records = Logger.Collector.GetSnapshot();
        Assert.Empty(records);
    }

    [Fact]
    public async Task ResetPasswordAttemptsAsync_WhenTheBoundElapses_LogsTheFailureAndReturns()
    {
        MockPasswordAttemptLimiter
            .Setup(x => x.ResetAsync(TenantId, UserName, It.IsAny<CancellationToken>()))
            .Returns(
                (string _, string _, CancellationToken x) =>
                {
                    TimeProvider.Advance(PostCommitWork.Timeout);
                    return x.IsCancellationRequested
                        ? ValueTask.FromCanceled(x)
                        : ValueTask.CompletedTask;
                }
            )
            .Verifiable();

        await Effects.ResetPasswordAttemptsAsync(TenantId, UserName);

        var snapshot = Logger.Collector.GetSnapshot();
        var record = Assert.Single(snapshot);
        Assert.Equal(EventIds.PostCommitPasswordAttemptResetFailed, record.Id.Id);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.IsAssignableFrom<OperationCanceledException>(record.Exception);
    }

    [Fact]
    public async Task ResetPasswordAttemptsAsync_WhenTheLimiterThrows_LogsAWarningAndDoesNotThrow()
    {
        MockPasswordAttemptLimiter
            .Setup(x => x.ResetAsync(TenantId, UserName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException())
            .Verifiable();

        await Effects.ResetPasswordAttemptsAsync(TenantId, UserName);

        var snapshot = Logger.Collector.GetSnapshot();
        var record = Assert.Single(snapshot);
        Assert.Equal(EventIds.PostCommitPasswordAttemptResetFailed, record.Id.Id);
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.IsAssignableFrom<InvalidOperationException>(record.Exception);
    }

    #endregion
}
