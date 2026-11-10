// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Options;
using NCode.Identity.SharedState.Valkey.Facades;
using NCode.Identity.SharedState.Valkey.Options;
using StackExchange.Redis;

namespace NCode.Identity.SharedState.Valkey;

/// <summary>
/// Provides the Valkey implementation of <see cref="IExpiringKeySet"/>, shared by every replica connected to the same
/// server. An add is one <c>SET key value NX PX ttl</c> command, which the server applies atomically, so exactly one of
/// any number of concurrent adds for a key succeeds across every replica; the server removes the key when its
/// time-to-live elapses. The time-to-live is computed from this replica's clock, so the expiry does not depend on the
/// server's clock agreeing with it. A command against an unreachable server throws rather than reporting the key as
/// absent, so a single-use check fails closed.
/// </summary>
internal class ValkeyExpiringKeySet(
    IValkeyConnection connection,
    IOptions<ValkeyOptions> optionsAccessor,
    TimeProvider timeProvider,
    ValkeySharedStateMetrics metrics
) : IExpiringKeySet
{
    private static RedisValue Marker => 1;

    private IValkeyConnection Connection { get; } = connection;

    private string KeyPrefix { get; } = optionsAccessor.Value.KeyPrefix;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ValkeySharedStateMetrics Metrics { get; } = metrics;

    /// <inheritdoc />
    public async ValueTask<bool> TryAddAsync(
        string key,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(key);

        var timeToLive = expiresWhen - TimeProvider.GetUtcNow();
        if (timeToLive <= TimeSpan.Zero)
            return false;

        // The server counts a time-to-live in whole milliseconds and rejects zero, so round up.
        var expiration = new Expiration(
            TimeSpan.FromMilliseconds(Math.Ceiling(timeToLive.TotalMilliseconds))
        );

        return await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.KeySetTryAddOperation,
            async cancellationToken =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                return await database
                    .StringSetAsync(GetKey(key), Marker, expiration, ValueCondition.NotExists)
                    .WaitAsync(cancellationToken);
            },
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<bool> ContainsAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        return await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.KeySetContainsOperation,
            async cancellationToken =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                return await database.KeyExistsAsync(GetKey(key)).WaitAsync(cancellationToken);
            },
            cancellationToken
        );
    }

    private RedisKey GetKey(string key) => KeyPrefix + key;
}
