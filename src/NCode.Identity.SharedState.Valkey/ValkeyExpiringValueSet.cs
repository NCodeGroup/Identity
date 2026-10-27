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

using System.Buffers.Binary;
using Microsoft.Extensions.Options;
using NCode.Identity.SharedState.Valkey.Facades;
using NCode.Identity.SharedState.Valkey.Options;
using StackExchange.Redis;

namespace NCode.Identity.SharedState.Valkey;

/// <summary>
/// Provides the Valkey implementation of <see cref="IExpiringValueSet"/>, shared by every replica connected to the
/// same server. A value is stored as one string: an 8-byte big-endian version followed by the value's bytes. An add is
/// <c>SET</c> with <c>NX</c> and a millisecond expiry, so at most one concurrent add succeeds. A replace is one script
/// that compares the stored version and, only when it matches, runs <c>SET</c> with <c>PX</c>; the server runs a script
/// atomically, so at most one concurrent replace of a version succeeds. A take is <c>GETDEL</c>, so at most one
/// concurrent take returns the value. <c>GetAsync</c> runs <c>GET</c> and <c>RemoveAsync</c> runs <c>DEL</c>. The
/// time-to-live is computed from this replica's clock, so the expiry does not depend on the server's clock agreeing
/// with it. A command against an unreachable server throws rather than reporting an absent value, so the caller fails
/// closed.
/// </summary>
internal class ValkeyExpiringValueSet(
    IValkeyConnection connection,
    IOptions<ValkeyOptions> optionsAccessor,
    TimeProvider timeProvider,
    ValkeySharedStateMetrics metrics
) : IExpiringValueSet
{
    private const int VersionLength = sizeof(long);

    /// <summary>
    /// The script that replaces <c>KEYS[1]</c> with <c>ARGV[2]</c> and expires it after <c>ARGV[3]</c> milliseconds,
    /// only when the stored version prefix equals <c>ARGV[1]</c>. It returns <c>1</c> when it replaced the value.
    /// </summary>
    internal const string ReplaceScript = """
        local current = redis.call('GET', KEYS[1])
        if not current or string.sub(current, 1, 8) ~= ARGV[1] then return 0 end
        redis.call('SET', KEYS[1], ARGV[2], 'PX', ARGV[3])
        return 1
        """;

    private IValkeyConnection Connection { get; } = connection;

    private string KeyPrefix { get; } = optionsAccessor.Value.KeyPrefix;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ValkeySharedStateMetrics Metrics { get; } = metrics;

    /// <summary>
    /// Encodes a version and a value as the string stored on the server.
    /// </summary>
    internal static byte[] Encode(long version, ReadOnlySpan<byte> value)
    {
        var stored = new byte[VersionLength + value.Length];
        BinaryPrimitives.WriteInt64BigEndian(stored, version);
        value.CopyTo(stored.AsSpan(VersionLength));
        return stored;
    }

    /// <summary>
    /// Decodes a string stored on the server into its value and version.
    /// </summary>
    internal static ExpiringValue Decode(byte[] stored)
    {
        if (stored.Length < VersionLength)
            throw new InvalidOperationException("The stored value is shorter than its version.");

        var version = BinaryPrimitives.ReadInt64BigEndian(stored);
        return new ExpiringValue(stored.AsMemory(VersionLength), version);
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryAddAsync(
        string key,
        ReadOnlyMemory<byte> value,
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
        var stored = Encode(1, value.Span);

        return await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.ValueSetTryAddOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                return await database
                    .StringSetAsync(GetKey(key), stored, expiration, ValueCondition.NotExists)
                    .WaitAsync(cancellationToken);
            }
        );
    }

    /// <inheritdoc />
    public async ValueTask<ExpiringValue?> GetAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        return await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.ValueSetGetOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                var value = await database.StringGetAsync(GetKey(key)).WaitAsync(cancellationToken);

                return value.IsNull ? (ExpiringValue?)null : Decode(ToBytes(value));
            }
        );
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryReplaceAsync(
        string key,
        long expectedVersion,
        ReadOnlyMemory<byte> value,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(key);

        var timeToLive = expiresWhen - TimeProvider.GetUtcNow();
        if (timeToLive <= TimeSpan.Zero)
            return false;

        // The server counts a time-to-live in whole milliseconds and rejects zero, so round up.
        var milliseconds = (long)Math.Ceiling(timeToLive.TotalMilliseconds);
        var expectedPrefix = Encode(expectedVersion, []);
        var stored = Encode(expectedVersion + 1, value.Span);

        return await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.ValueSetTryReplaceOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                var result = await database
                    .ScriptEvaluateAsync(
                        ReplaceScript,
                        [GetKey(key)],
                        [expectedPrefix, stored, milliseconds]
                    )
                    .WaitAsync(cancellationToken);

                return (long)result == 1;
            }
        );
    }

    /// <inheritdoc />
    public async ValueTask<ExpiringValue?> TryTakeAsync(
        string key,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(key);

        return await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.ValueSetTryTakeOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                var value = await database
                    .StringGetDeleteAsync(GetKey(key))
                    .WaitAsync(cancellationToken);

                return value.IsNull ? (ExpiringValue?)null : Decode(ToBytes(value));
            }
        );
    }

    /// <inheritdoc />
    public async ValueTask RemoveAsync(string key, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(key);

        await Metrics.MeasureAsync(
            ValkeySharedStateMetrics.ValueSetRemoveOperation,
            async () =>
            {
                var database = await Connection.GetDatabaseAsync(cancellationToken);
                await database.KeyDeleteAsync(GetKey(key)).WaitAsync(cancellationToken);
            }
        );
    }

    private static byte[] ToBytes(RedisValue value) =>
        (byte[]?)value ?? throw new InvalidOperationException("The stored value is absent.");

    private RedisKey GetKey(string key) => KeyPrefix + key;
}
