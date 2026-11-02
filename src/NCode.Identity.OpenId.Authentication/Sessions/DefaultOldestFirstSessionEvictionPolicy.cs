// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// The default <see cref="ISessionEvictionPolicy"/> that evicts the subject's oldest sessions, keeping the most recent
/// ones. It keeps the newest <c>max - 1</c> sessions so that adding a new session leaves the subject at the limit.
/// </summary>
internal class DefaultOldestFirstSessionEvictionPolicy : ISessionEvictionPolicy
{
    /// <inheritdoc />
    public IEnumerable<SessionInfo> SelectSessionsToEvict(
        IReadOnlyList<SessionInfo> existingSessions,
        int maxConcurrentSessions
    ) =>
        existingSessions
            .OrderByDescending(session => session.CreatedWhen)
            .Skip(maxConcurrentSessions - 1);
}
