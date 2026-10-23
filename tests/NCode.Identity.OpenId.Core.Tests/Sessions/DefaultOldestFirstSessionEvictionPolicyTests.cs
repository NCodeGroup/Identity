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
