#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using JetBrains.Annotations;
using NCode.Identity.OpenId.Authentication.Persistence;
using NCode.Identity.OpenId.Contexts;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Provides an abstraction for managing persisted end-user sessions, including creating a session at sign-in, resolving
/// a session by its <c>sid</c>, and tracking the relying parties a session has issued tokens to. A session is revoked
/// at logout by the session-scoped grant revocation (<see cref="NCode.Identity.OpenId.Persistence.Stores"/>), which
/// revokes the session grant alongside the grants issued under it.
/// </summary>
[PublicAPI]
public interface ISessionService
{
    /// <summary>
    /// Creates and persists a new end-user session and returns its opaque session identifier (<c>sid</c>).
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="request">The details used to create the session.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the opaque session
    /// identifier (<c>sid</c>).</returns>
    ValueTask<string> CreateAsync(
        OpenIdContext openIdContext,
        CreateSessionRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Attempts to retrieve a persisted session by its opaque session identifier (<c>sid</c>).
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="sessionId">The opaque session identifier (<c>sid</c>).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the persisted
    /// session if found; otherwise <c>null</c>.</returns>
    ValueTask<PersistedGrant<SessionGrant>?> GetOrDefaultAsync(
        OpenIdContext openIdContext,
        string sessionId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that the specified client (relying party) has participated in the session, so it is notified on
    /// back-channel logout. This is idempotent.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="sessionId">The opaque session identifier (<c>sid</c>).</param>
    /// <param name="clientId">The identifier of the participating client.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when this
    /// call added the client; <c>false</c> when the session is not active or already lists the client.</returns>
    ValueTask<bool> AddClientAsync(
        OpenIdContext openIdContext,
        string sessionId,
        string clientId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validates that the session identified by <paramref name="sessionId"/> is still active and has not ended. When the
    /// session is still valid its last-activity marker is advanced and its expiry slides; when it has ended, at its
    /// absolute end or at the end of its idle window, it is terminated. This is the server-authoritative session check
    /// performed when an authenticated request resolves a session.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="sessionId">The opaque session identifier (<c>sid</c>).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when the
    /// session remains valid; otherwise <c>false</c> (missing, revoked, expired, or idle-timed-out).</returns>
    ValueTask<bool> ValidateAndTouchAsync(
        OpenIdContext openIdContext,
        string sessionId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Terminates the session identified by <paramref name="sessionId"/> through the session-termination seam when it
    /// has ended, at its absolute end or at the end of its idle window, unless another caller terminated it. Of the
    /// requests and background passes that find one ended session, only one terminates it.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="sessionId">The opaque session identifier (<c>sid</c>).</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when this
    /// call terminated the session; <c>false</c> when it is missing, revoked, or has not ended.</returns>
    ValueTask<bool> TerminateIfEndedAsync(
        OpenIdContext openIdContext,
        string sessionId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Enumerates the active sessions the specified subject currently holds, newest first. This backs the
    /// active-session listing surface and the concurrent-session-limit enforcement.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="subjectId">The stable, server-owned principal identifier whose sessions are enumerated.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the subject's active
    /// sessions ordered newest first.</returns>
    ValueTask<IReadOnlyList<SessionInfo>> GetSessionsAsync(
        OpenIdContext openIdContext,
        string subjectId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Terminates a single session, dispatching the session-termination side effects (grant revocation, back-channel
    /// logout notification, and auditing) through the <see cref="OnSessionTerminatedCommand"/> seam. Other sessions the
    /// subject holds are left intact. Of the callers that terminate one session at the same time, only one terminates
    /// it.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="sessionId">The opaque session identifier (<c>sid</c>) to terminate.</param>
    /// <param name="reason">The reason the session is being terminated: one of <see cref="SessionTerminationReasons"/> or
    /// an extension's own name.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing <c>true</c> when this
    /// call terminated the session; <c>false</c> when the session does not exist or is already revoked, including by a
    /// concurrent caller.</returns>
    ValueTask<bool> RevokeAsync(
        OpenIdContext openIdContext,
        string sessionId,
        string reason,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Terminates every session the specified subject holds ("sign out everywhere") and revokes every grant the subject
    /// holds, dispatching the session-termination side effects through the <see cref="OnSessionTerminatedCommand"/>
    /// seam as a single subject-wide termination.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="subjectId">The stable, server-owned principal identifier whose sessions are terminated.</param>
    /// <param name="reason">The reason the sessions are being terminated: one of <see cref="SessionTerminationReasons"/> or
    /// an extension's own name.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the number of sessions
    /// this call terminated; a session a concurrent caller terminated first is not counted.</returns>
    /// <exception cref="ArgumentException">The <paramref name="subjectId"/> is null or empty.</exception>
    ValueTask<int> RevokeAllAsync(
        OpenIdContext openIdContext,
        string subjectId,
        string reason,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Terminates every session the specified subject holds <em>except</em> the one identified by
    /// <paramref name="exceptSessionId"/> ("sign out everywhere else"), terminating each remaining session individually
    /// through the <see cref="OnSessionTerminatedCommand"/> seam so the retained session and the grants issued under it
    /// stay intact.
    /// </summary>
    /// <param name="openIdContext">The <see cref="OpenIdContext"/> instance associated with the current request.</param>
    /// <param name="subjectId">The stable, server-owned principal identifier whose sessions are terminated.</param>
    /// <param name="exceptSessionId">The opaque session identifier (<c>sid</c>) to retain.</param>
    /// <param name="reason">The reason the sessions are being terminated: one of <see cref="SessionTerminationReasons"/> or
    /// an extension's own name.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the number of sessions
    /// this call terminated; a session a concurrent caller terminated first is not counted.</returns>
    /// <exception cref="ArgumentException">The <paramref name="subjectId"/> is null or empty.</exception>
    ValueTask<int> RevokeAllExceptAsync(
        OpenIdContext openIdContext,
        string subjectId,
        string exceptSessionId,
        string reason,
        CancellationToken cancellationToken
    );
}
