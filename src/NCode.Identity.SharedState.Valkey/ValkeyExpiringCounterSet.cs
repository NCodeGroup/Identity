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
/// Provides the Valkey implementation of <see cref="IExpiringCounterSet"/>, shared by every replica connected to the
/// same server. An increment is one script that runs <c>INCR</c> and, when that starts the counter, <c>PEXPIRE</c>; the
/// server runs a script atomically, so every concurrent increment across every replica is counted once and a counter
/// never outlives its window. The time-to-live is computed from this replica's clock, so the expiry does not depend on
/// the server's clock agreeing with it. A batch is one script that increments each counter in order and stops after the
/// first counter that is over its limit, so one round trip serves every counter the batch names. A command against an
/// unreachable server throws rather than reporting a count, so the caller decides how a limit behaves while the server
/// is down. <c>RemoveAsync</c> runs <c>DEL</c> on the counter's key.
/// </summary>
internal class ValkeyExpiringCounterSet(
    IValkeyConnection connection,
    IOptions<ValkeyOptions> optionsAccessor,
    TimeProvider timeProvider,
    ValkeySharedStateMetrics metrics
) : IExpiringCounterSet
{
    /// <summary>
    /// The script that increments <c>KEYS[1]</c> and, when the increment starts the counter, expires it after
    /// <c>ARGV[1]</c> milliseconds.
    /// </summary>
    internal const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('PEXPIRE', KEYS[1], ARGV[1])
        end
        return count
        """;

    /// <summary>
    /// The script that increments a batch of counters in order and stops after the first one that is over its limit.
    /// <c>KEYS</c> holds two keys per counter, the counter and its reference; <c>ARGV</c> holds three values per
    /// counter, the time-to-live in milliseconds, the reference weight and the limit. It returns two values per counter
    /// reached: the count and the reference count.
    /// </summary>
    internal const string IncrementUntilExceededScript = """
        local results = {}
        for i = 1, #KEYS / 2 do
            local key = KEYS[2 * i - 1]
            local ttl = tonumber(ARGV[3 * i - 2])
            local weight = tonumber(ARGV[3 * i - 1])
            local count = 0
            if ttl > 0 then
                count = redis.call('INCR', key)
                if count == 1 then redis.call('PEXPIRE', key, ttl) end
            end
            local reference = 0
            if weight > 0 then reference = tonumber(redis.call('GET', KEYS[2 * i]) or '0') end
            results[2 * i - 1] = count
            results[2 * i] = reference
            if count + reference * weight > tonumber(ARGV[3 * i]) then break end
        end
        return results
        """;

    private IValkeyConnection Connection { get; } = connection;

    private string KeyPrefix { get; } = optionsAccessor.Value.KeyPrefix;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ValkeySharedStateMetrics Metrics { get; } = metrics;

    /// <inheritdoc />
    public async ValueTask<long> IncrementAsync(
        string key,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(key);

        var timeToLive = expiresWhen - TimeProvider.GetUtcNow();
        if (timeToLive <= TimeSpan.Zero)
            return 0L;

        // The server counts a time-to-live in whole milliseconds and rejects zero, so round up.
        var milliseconds = (long)Math.Ceiling(timeToLive.TotalMilliseconds);

        return await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.CounterIncrementOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                var result = await database
                    .ScriptEvaluateAsync(IncrementScript, [GetKey(key)], [milliseconds])
                    .WaitAsync(cancellationToken);

                return (long)result;
            }
        );
    }

    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<ExpiringCounterCount>> IncrementUntilExceededAsync(
        IReadOnlyList<ExpiringCounterIncrement> increments,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(increments);

        if (increments.Count == 0)
            return [];

        var now = TimeProvider.GetUtcNow();
        var keys = new RedisKey[2 * increments.Count];
        var values = new RedisValue[3 * increments.Count];
        for (var i = 0; i < increments.Count; ++i)
        {
            var increment = increments[i];
            var referenceKey = increment.ReferenceWeight > 0 ? increment.ReferenceKey : null;
            var timeToLive = increment.ExpiresWhen - now;

            keys[2 * i] = GetKey(increment.Key);
            keys[2 * i + 1] = GetKey(referenceKey ?? increment.Key);

            // The server counts a time-to-live in whole milliseconds and rejects zero, so round up.
            values[3 * i] =
                timeToLive > TimeSpan.Zero ? (long)Math.Ceiling(timeToLive.TotalMilliseconds) : 0L;
            values[3 * i + 1] = referenceKey is null ? 0d : increment.ReferenceWeight;
            values[3 * i + 2] = increment.Limit;
        }

        var results = await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.CounterIncrementUntilExceededOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                var result = await database
                    .ScriptEvaluateAsync(IncrementUntilExceededScript, keys, values)
                    .WaitAsync(cancellationToken);

                return (long[]?)result ?? [];
            }
        );

        var counts = new List<ExpiringCounterCount>(results.Length / 2);
        for (var i = 0; i + 1 < results.Length; i += 2)
            counts.Add(new ExpiringCounterCount(results[i], results[i + 1]));

        return counts;
    }

    /// <inheritdoc />
    public async ValueTask<long> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        return await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.CounterGetOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                var value = await database.StringGetAsync(GetKey(key)).WaitAsync(cancellationToken);

                return value.IsNull ? 0L : (long)value;
            }
        );
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.CounterRemoveOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                await database.KeyDeleteAsync(GetKey(key)).WaitAsync(cancellationToken);
            }
        );
    }

    private RedisKey GetKey(string key) => KeyPrefix + key;
}
