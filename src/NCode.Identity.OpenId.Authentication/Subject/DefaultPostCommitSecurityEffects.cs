// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Authentication.Sessions;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.Persistence;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Provides the default implementation of <see cref="IPostCommitSecurityEffects"/> that runs each effect in its
/// own <see cref="PostCommitWork.RunAsync{TState}"/> step, logs a failure and never throws it.
/// </summary>
// See ADR-1468.
[SuppressMessage(
    "Design",
    "NCID0003",
    Justification = "The work follows a committed change and runs on its own bounded token, so no caller can pass a token that drops it (ADR-1351, ADR-1464, ADR-1468)."
)]
internal sealed class DefaultPostCommitSecurityEffects(
    ISessionService sessionService,
    IPasswordAttemptLimiter passwordAttemptLimiter,
    TimeProvider timeProvider,
    ILogger<DefaultPostCommitSecurityEffects> logger
) : IPostCommitSecurityEffects
{
    private ISessionService SessionService { get; } = sessionService;
    private IPasswordAttemptLimiter PasswordAttemptLimiter { get; } = passwordAttemptLimiter;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<DefaultPostCommitSecurityEffects> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask TerminateSessionsAsync(
        OpenIdContext openIdContext,
        string subjectId,
        string reason
    )
    {
        try
        {
            await PostCommitWork.RunAsync(
                TimeProvider,
                static async (state, cancellationToken) =>
                {
                    await state.SessionService.RevokeAllAsync(
                        state.OpenIdContext,
                        state.SubjectId,
                        state.Reason,
                        cancellationToken
                    );
                },
                (
                    SessionService: SessionService,
                    OpenIdContext: openIdContext,
                    SubjectId: subjectId,
                    Reason: reason
                )
            );
        }
        catch (Exception exception)
        {
            // The change is committed and no caller token reaches the effect, so every failure, its bound
            // included, is logged.
            var tenantId = openIdContext.Tenant.TenantId;
            Logger.PostCommitSessionTerminationFailed(exception, tenantId, subjectId);
        }
    }

    /// <inheritdoc />
    public async ValueTask TerminateOtherSessionsAsync(
        OpenIdContext openIdContext,
        string subjectId,
        string currentSessionId,
        string reason
    )
    {
        try
        {
            await PostCommitWork.RunAsync(
                TimeProvider,
                static async (state, cancellationToken) =>
                {
                    await state.SessionService.RevokeAllExceptAsync(
                        state.OpenIdContext,
                        state.SubjectId,
                        state.CurrentSessionId,
                        state.Reason,
                        cancellationToken
                    );
                },
                (
                    SessionService: SessionService,
                    OpenIdContext: openIdContext,
                    SubjectId: subjectId,
                    CurrentSessionId: currentSessionId,
                    Reason: reason
                )
            );
        }
        catch (Exception exception)
        {
            // The change is committed and no caller token reaches the effect, so every failure, its bound
            // included, is logged.
            var tenantId = openIdContext.Tenant.TenantId;
            Logger.PostCommitSessionTerminationFailed(exception, tenantId, subjectId);
        }
    }

    /// <inheritdoc />
    public async ValueTask ResetPasswordAttemptsAsync(string tenantId, string userName)
    {
        try
        {
            await PostCommitWork.RunAsync(
                TimeProvider,
                static async (state, cancellationToken) =>
                {
                    await state.PasswordAttemptLimiter.ResetAsync(
                        state.TenantId,
                        state.UserName,
                        cancellationToken
                    );
                },
                (
                    PasswordAttemptLimiter: PasswordAttemptLimiter,
                    TenantId: tenantId,
                    UserName: userName
                )
            );
        }
        catch (Exception exception)
        {
            // The change is committed and no caller token reaches the effect, so every failure, its bound
            // included, is logged.
            Logger.PostCommitPasswordAttemptResetFailed(exception, tenantId);
        }
    }
}
