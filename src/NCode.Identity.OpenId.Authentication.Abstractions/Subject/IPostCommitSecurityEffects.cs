// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Subject;

/// <summary>
/// Runs the security effects that follow a committed change to an end user: ending its sessions and clearing its
/// failed password attempts. Each effect runs on its own token, bounded by <c>PostCommitWork.Timeout</c>, so the
/// request's cancellation reaches none of it and a hung store cannot hold the response; an effect that fails or times
/// out is logged and never thrown, so the committed change answers success and its audit record follows.
/// </summary>
// See ADR-1468.
[PublicAPI]
public interface IPostCommitSecurityEffects
{
    /// <summary>
    /// Terminates every session of the subject through <c>ISessionService.RevokeAllAsync</c>.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="subjectId">The identifier of the subject whose sessions end.</param>
    /// <param name="reason">The reason that is recorded for the termination.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask TerminateSessionsAsync(OpenIdContext openIdContext, string subjectId, string reason);

    /// <summary>
    /// Terminates every session of the subject except the current one through <c>ISessionService.RevokeAllExceptAsync</c>.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="subjectId">The identifier of the subject whose other sessions end.</param>
    /// <param name="currentSessionId">The identifier of the session that stays active.</param>
    /// <param name="reason">The reason that is recorded for the termination.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask TerminateOtherSessionsAsync(
        OpenIdContext openIdContext,
        string subjectId,
        string currentSessionId,
        string reason
    );

    /// <summary>
    /// Clears the failed password attempts of the account that the user name names through
    /// <c>IPasswordAttemptLimiter.ResetAsync</c>.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant that the account belongs to.</param>
    /// <param name="userName">The user name of the account.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask ResetPasswordAttemptsAsync(string tenantId, string userName);
}
