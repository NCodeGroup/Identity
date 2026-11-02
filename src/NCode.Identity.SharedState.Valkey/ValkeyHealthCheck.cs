// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Diagnostics.HealthChecks;
using NCode.Identity.SharedState.Valkey.Facades;
using StackExchange.Redis;

namespace NCode.Identity.SharedState.Valkey;

/// <summary>
/// Reports whether the Valkey server answers a <c>PING</c>. A replica that cannot reach it cannot check or record a
/// single-use key, so it is not ready to serve the requests that depend on one.
/// </summary>
internal class ValkeyHealthCheck(IValkeyConnection connection) : IHealthCheck
{
    internal const string UnreachableDescription = "The Valkey server is unreachable.";

    private IValkeyConnection Connection { get; } = connection;

    /// <inheritdoc />
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var database = await Connection.GetDatabaseAsync(cancellationToken);
            await database.PingAsync().WaitAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is RedisException or TimeoutException)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                UnreachableDescription,
                exception
            );
        }
    }
}
