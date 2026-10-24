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
using Moq;
using NCode.Identity.SharedState.Valkey.Facades;
using StackExchange.Redis;
using Xunit;

namespace NCode.Identity.SharedState.Valkey.Tests;

public sealed class ValkeyHealthCheckTests : BaseTests
{
    private Mock<IValkeyConnection> MockConnection { get; }

    private Mock<IDatabase> MockDatabase { get; }

    private ValkeyHealthCheck HealthCheck { get; }

    private HealthCheckContext Context { get; }

    public ValkeyHealthCheckTests()
    {
        MockConnection = CreateStrictMock<IValkeyConnection>();
        MockDatabase = CreateStrictMock<IDatabase>();
        HealthCheck = new ValkeyHealthCheck(MockConnection.Object);
        Context = new HealthCheckContext
        {
            Registration = new HealthCheckRegistration(
                DefaultRegistration.HealthCheckName,
                HealthCheck,
                HealthStatus.Degraded,
                [HealthCheckTags.Readiness]
            ),
        };

        MockConnection
            .Setup(x => x.GetDatabaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockDatabase.Object)
            .Verifiable();
    }

    #region CheckHealthAsync Tests

    [Fact]
    public async Task CheckHealthAsync_WhenServerAnswers_ReturnsHealthy()
    {
        MockDatabase
            .Setup(x => x.PingAsync(CommandFlags.None))
            .ReturnsAsync(TimeSpan.FromMilliseconds(1))
            .Verifiable();

        var result = await HealthCheck.CheckHealthAsync(Context, CancellationToken.None);

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServerUnreachable_ReturnsTheFailureStatus()
    {
        var exception = new RedisConnectionException(
            ConnectionFailureType.UnableToConnect,
            CommandFlags.None,
            "down"
        );
        MockDatabase.Setup(x => x.PingAsync(CommandFlags.None)).ThrowsAsync(exception).Verifiable();

        var result = await HealthCheck.CheckHealthAsync(Context, CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
        Assert.Equal(ValkeyHealthCheck.UnreachableDescription, result.Description);
        Assert.Same(exception, result.Exception);
    }

    [Fact]
    public async Task CheckHealthAsync_WhenServerTimesOut_ReturnsTheFailureStatus()
    {
        MockDatabase
            .Setup(x => x.PingAsync(CommandFlags.None))
            .ThrowsAsync(new RedisTimeoutException(CommandFlags.None, "slow", CommandStatus.Sent))
            .Verifiable();

        var result = await HealthCheck.CheckHealthAsync(Context, CancellationToken.None);

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    #endregion
}
