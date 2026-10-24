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

using System.Diagnostics;
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
/// the server's clock agreeing with it. A command against an unreachable server throws rather than reporting a count,
/// so the caller decides how a limit behaves while the server is down. <c>RemoveAsync</c> runs <c>DEL</c> on the
/// counter's key.
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

        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var database = await Connection.GetDatabaseAsync(cancellationToken);
            var result = await database
                .ScriptEvaluateAsync(IncrementScript, [GetKey(key)], [milliseconds])
                .WaitAsync(cancellationToken);

            Metrics.OperationCompleted(
                ValkeySharedStateMetrics.CounterIncrementOperation,
                startTimestamp,
                exception: null
            );
            return (long)result;
        }
        catch (Exception exception)
        {
            Metrics.OperationCompleted(
                ValkeySharedStateMetrics.CounterIncrementOperation,
                startTimestamp,
                exception
            );
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask<long> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var database = await Connection.GetDatabaseAsync(cancellationToken);
            var value = await database.StringGetAsync(GetKey(key)).WaitAsync(cancellationToken);

            Metrics.OperationCompleted(
                ValkeySharedStateMetrics.CounterGetOperation,
                startTimestamp,
                exception: null
            );
            return value.IsNull ? 0L : (long)value;
        }
        catch (Exception exception)
        {
            Metrics.OperationCompleted(
                ValkeySharedStateMetrics.CounterGetOperation,
                startTimestamp,
                exception
            );
            throw;
        }
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        var startTimestamp = Stopwatch.GetTimestamp();
        try
        {
            var database = await Connection.GetDatabaseAsync(cancellationToken);
            await database.KeyDeleteAsync(GetKey(key)).WaitAsync(cancellationToken);

            Metrics.OperationCompleted(
                ValkeySharedStateMetrics.CounterRemoveOperation,
                startTimestamp,
                exception: null
            );
        }
        catch (Exception exception)
        {
            Metrics.OperationCompleted(
                ValkeySharedStateMetrics.CounterRemoveOperation,
                startTimestamp,
                exception
            );
            throw;
        }
    }

    private RedisKey GetKey(string key) => KeyPrefix + key;
}
