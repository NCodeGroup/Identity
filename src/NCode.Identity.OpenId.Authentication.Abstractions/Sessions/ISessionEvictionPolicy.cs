// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Decides which of a subject's existing sessions to evict when a new sign-in would exceed the configured
/// concurrent-session limit. The default policy evicts the oldest sessions; replace the registered implementation to
/// evict by a different strategy (for example, least-recently-active, or preserving multi-factor sessions).
/// </summary>
[PublicAPI]
public interface ISessionEvictionPolicy
{
    /// <summary>
    /// Selects the sessions to evict so that, after a new session is added, the subject holds at most
    /// <paramref name="maxConcurrentSessions"/> active sessions.
    /// </summary>
    /// <param name="existingSessions">The subject's current active sessions.</param>
    /// <param name="maxConcurrentSessions">The maximum number of simultaneous sessions the subject may hold
    /// (guaranteed greater than zero).</param>
    /// <returns>The sessions to evict; may be empty when the subject is below the limit.</returns>
    IEnumerable<SessionInfo> SelectSessionsToEvict(
        IReadOnlyList<SessionInfo> existingSessions,
        int maxConcurrentSessions
    );
}
