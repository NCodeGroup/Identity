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
