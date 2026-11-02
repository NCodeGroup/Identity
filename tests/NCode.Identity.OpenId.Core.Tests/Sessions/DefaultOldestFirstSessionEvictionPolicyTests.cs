// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using NCode.Identity.OpenId.Authentication.Sessions;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public class DefaultOldestFirstSessionEvictionPolicyTests
{
    private static readonly DateTimeOffset Epoch = DateTimeOffset.Parse(
        "2026-01-01T00:00:00Z",
        CultureInfo.InvariantCulture
    );

    private readonly DefaultOldestFirstSessionEvictionPolicy _policy = new();

    private static SessionInfo CreateSession(string sessionId, DateTimeOffset createdWhen) =>
        new(
            sessionId,
            "subject-1",
            createdWhen,
            ExpiresWhen: createdWhen.AddDays(3),
            new SessionGrant
            {
                AuthTime = createdWhen,
                ClientIds = [],
                LastActivityWhen = createdWhen,
            }
        );

    [Fact]
    public void SelectSessionsToEvict_WhenOverLimit_EvictsOldestKeepingMaxMinusOneNewest()
    {
        var sessions = new[]
        {
            CreateSession("newest", Epoch.AddMinutes(30)),
            CreateSession("middle", Epoch.AddMinutes(20)),
            CreateSession("oldest", Epoch.AddMinutes(10)),
        };

        // With a cap of 2, adding a new session should leave 2 active, so keep the 1 newest and evict the other 2.
        var evicted = _policy.SelectSessionsToEvict(sessions, maxConcurrentSessions: 2).ToList();

        Assert.Equal(["middle", "oldest"], evicted.Select(session => session.SessionId));
    }

    [Fact]
    public void SelectSessionsToEvict_WhenUnderLimit_EvictsNothing()
    {
        var sessions = new[] { CreateSession("only", Epoch.AddMinutes(10)) };

        var evicted = _policy.SelectSessionsToEvict(sessions, maxConcurrentSessions: 5).ToList();

        Assert.Empty(evicted);
    }
}
