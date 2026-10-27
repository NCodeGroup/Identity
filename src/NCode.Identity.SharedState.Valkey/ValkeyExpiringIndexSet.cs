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

using Microsoft.Extensions.Options;
using NCode.Identity.SharedState.Valkey.Facades;
using NCode.Identity.SharedState.Valkey.Options;
using StackExchange.Redis;

namespace NCode.Identity.SharedState.Valkey;

/// <summary>
/// Provides the Valkey implementation of <see cref="IExpiringIndexSet"/>, shared by every replica connected to the
/// same server. An index is one sorted set whose members are the index members and whose score is the member's expiry
/// in Unix milliseconds. An add is one script that prunes the expired members, sets the member's score and extends the
/// key's time-to-live to cover the latest member; the server runs a script atomically. A read returns the members whose
/// score is after the current time, so member expiry is judged on the clock of the replica that reads. A failed
/// command throws rather than reporting an empty index, so the caller fails closed.
/// </summary>
internal class ValkeyExpiringIndexSet(
    IValkeyConnection connection,
    IOptions<ValkeyOptions> optionsAccessor,
    TimeProvider timeProvider,
    ValkeySharedStateMetrics metrics
) : IExpiringIndexSet
{
    /// <summary>
    /// The script that prunes the expired members of <c>KEYS[1]</c> (score at or before <c>ARGV[1]</c>), adds the member
    /// <c>ARGV[3]</c> with the score <c>ARGV[2]</c>, and extends the key's time-to-live to at least <c>ARGV[4]</c>
    /// milliseconds.
    /// </summary>
    internal const string AddMemberScript = """
        redis.call('ZREMRANGEBYSCORE', KEYS[1], '-inf', ARGV[1])
        redis.call('ZADD', KEYS[1], ARGV[2], ARGV[3])
        local ttl = tonumber(ARGV[4])
        if redis.call('PTTL', KEYS[1]) < ttl then redis.call('PEXPIRE', KEYS[1], ttl) end
        return 1
        """;

    private IValkeyConnection Connection { get; } = connection;

    private string KeyPrefix { get; } = optionsAccessor.Value.KeyPrefix;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ValkeySharedStateMetrics Metrics { get; } = metrics;

    /// <inheritdoc />
    public async ValueTask AddMemberAsync(
        string indexKey,
        string member,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(indexKey);
        ArgumentNullException.ThrowIfNull(member);

        var now = TimeProvider.GetUtcNow();
        var timeToLive = expiresWhen - now;
        if (timeToLive <= TimeSpan.Zero)
            return;

        // The server counts a time-to-live in whole milliseconds and rejects zero, so round up.
        var milliseconds = (long)Math.Ceiling(timeToLive.TotalMilliseconds);
        RedisValue[] values =
        [
            now.ToUnixTimeMilliseconds(),
            expiresWhen.ToUnixTimeMilliseconds(),
            member,
            milliseconds,
        ];

        await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.IndexSetAddMemberOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                await database
                    .ScriptEvaluateAsync(
                        AddMemberScript,
                        [GetKey(indexKey)],
                        values,
                        CommandFlags.None
                    )
                    .WaitAsync(cancellationToken);
            }
        );
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<string>> GetMembersAsync(
        string indexKey,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(indexKey);

        double start = TimeProvider.GetUtcNow().ToUnixTimeMilliseconds();

        return await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.IndexSetGetMembersOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                var values = await database
                    .SortedSetRangeByScoreAsync(
                        GetKey(indexKey),
                        start,
                        double.PositiveInfinity,
                        Exclude.Start,
                        Order.Ascending,
                        0,
                        -1,
                        CommandFlags.None
                    )
                    .WaitAsync(cancellationToken);

                var members = new string[values.Length];
                for (var i = 0; i < values.Length; i++)
                    members[i] =
                        (string?)values[i]
                        ?? throw new InvalidOperationException("A stored member is absent.");

                return (IReadOnlyList<string>)members;
            }
        );
    }

    /// <inheritdoc />
    public async ValueTask RemoveMemberAsync(
        string indexKey,
        string member,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(indexKey);
        ArgumentNullException.ThrowIfNull(member);

        await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.IndexSetRemoveMemberOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                await database
                    .SortedSetRemoveAsync(GetKey(indexKey), member, CommandFlags.None)
                    .WaitAsync(cancellationToken);
            }
        );
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string indexKey, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(indexKey);

        await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.IndexSetRemoveOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                await database
                    .KeyDeleteAsync(GetKey(indexKey), CommandFlags.None)
                    .WaitAsync(cancellationToken);
            }
        );
    }

    private RedisKey GetKey(string indexKey) => KeyPrefix + indexKey;
}
