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
