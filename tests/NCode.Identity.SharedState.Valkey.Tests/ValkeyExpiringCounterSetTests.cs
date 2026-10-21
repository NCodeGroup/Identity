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

using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Moq;
using NCode.Identity.SharedState.Valkey.Facades;
using NCode.Identity.SharedState.Valkey.Options;
using StackExchange.Redis;
using Xunit;

namespace NCode.Identity.SharedState.Valkey.Tests;

public sealed class ValkeyExpiringCounterSetTests : BaseTests
{
    private const string KeyPrefix = "test:";

    private static DateTimeOffset Now { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private Mock<IValkeyConnection> MockConnection { get; }

    private Mock<IDatabase> MockDatabase { get; }

    private Mock<TimeProvider> MockTimeProvider { get; }

    private ValkeyExpiringCounterSet CounterSet { get; }

    private ServiceProvider MetricsProvider { get; }

    private MetricCollector<double> DurationCollector { get; }

    public ValkeyExpiringCounterSetTests()
    {
        MockConnection = CreateStrictMock<IValkeyConnection>();
        MockDatabase = CreateStrictMock<IDatabase>();
        MockTimeProvider = CreateStrictMock<TimeProvider>();

        MetricsProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = MetricsProvider.GetRequiredService<IMeterFactory>();
        DurationCollector = new MetricCollector<double>(
            meterFactory,
            ValkeyMetrics.MeterName,
            ValkeySharedStateMetrics.OperationDurationName
        );

        CounterSet = new ValkeyExpiringCounterSet(
            MockConnection.Object,
            Microsoft.Extensions.Options.Options.Create(
                new ValkeyOptions { KeyPrefix = KeyPrefix }
            ),
            MockTimeProvider.Object,
            new ValkeySharedStateMetrics(meterFactory)
        );
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            DurationCollector.Dispose();
            MetricsProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    private void AssertOperationRecorded(string operation, string? errorType)
    {
        var measurement = Assert.Single(DurationCollector.GetMeasurementSnapshot());
        Assert.Equal("s", DurationCollector.Instrument?.Unit);
        Assert.True(measurement.Value >= 0);
        Assert.Equal(operation, measurement.Tags[ValkeySharedStateMetrics.OperationAttribute]);
        Assert.Equal(
            ValkeySharedStateMetrics.Adapter,
            measurement.Tags[ValkeySharedStateMetrics.AdapterAttribute]
        );
        if (errorType is null)
            Assert.DoesNotContain(
                ValkeySharedStateMetrics.ErrorTypeAttribute,
                measurement.Tags.Keys
            );
        else
            Assert.Equal(errorType, measurement.Tags[ValkeySharedStateMetrics.ErrorTypeAttribute]);
    }

    private void SetupNow() => MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();

    private void SetupDatabase() =>
        MockConnection
            .Setup(x => x.GetDatabaseAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockDatabase.Object)
            .Verifiable();

    private void SetupIncrement(long milliseconds, long count) =>
        MockDatabase
            .Setup(x =>
                x.ScriptEvaluateAsync(
                    ValkeyExpiringCounterSet.IncrementScript,
                    It.Is<RedisKey[]>(keys =>
                        keys.SequenceEqual(new RedisKey[] { KeyPrefix + "key" })
                    ),
                    It.Is<RedisValue[]>(values =>
                        values.SequenceEqual(new RedisValue[] { milliseconds })
                    ),
                    CommandFlags.None
                )
            )
            .ReturnsAsync(RedisResult.Create(count))
            .Verifiable();

    #region IncrementAsync Tests

    [Fact]
    public async Task IncrementAsync_RunsTheScriptOnThePrefixedKeyAndReturnsItsCount()
    {
        SetupNow();
        SetupDatabase();
        SetupIncrement(30_000, count: 3);

        var count = await CounterSet.IncrementAsync(
            "key",
            Now.AddSeconds(30),
            CancellationToken.None
        );

        Assert.Equal(3, count);
        AssertOperationRecorded(
            ValkeySharedStateMetrics.CounterIncrementOperation,
            errorType: null
        );
    }

    [Fact]
    public async Task IncrementAsync_WhenTimeToLiveHasAFractionalMillisecond_RoundsItUp()
    {
        SetupNow();
        SetupDatabase();
        SetupIncrement(1, count: 1);

        var count = await CounterSet.IncrementAsync(
            "key",
            Now.AddTicks(TimeSpan.TicksPerMillisecond / 2),
            CancellationToken.None
        );

        Assert.Equal(1, count);
    }

    [Fact]
    public async Task IncrementAsync_WhenExpiryNotInFuture_ReturnsZeroWithoutACommand()
    {
        SetupNow();

        var count = await CounterSet.IncrementAsync("key", Now, CancellationToken.None);

        Assert.Equal(0, count);
        Assert.Empty(DurationCollector.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task IncrementAsync_WhenServerUnreachable_Throws()
    {
        SetupNow();
        SetupDatabase();
        MockDatabase
            .Setup(x =>
                x.ScriptEvaluateAsync(
                    It.IsAny<string>(),
                    It.IsAny<RedisKey[]>(),
                    It.IsAny<RedisValue[]>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ThrowsAsync(
                new RedisConnectionException(
                    ConnectionFailureType.UnableToConnect,
                    CommandFlags.None,
                    "down"
                )
            )
            .Verifiable();

        await Assert.ThrowsAsync<RedisConnectionException>(async () =>
            await CounterSet.IncrementAsync("key", Now.AddSeconds(30), CancellationToken.None)
        );
        AssertOperationRecorded(
            ValkeySharedStateMetrics.CounterIncrementOperation,
            typeof(RedisConnectionException).FullName
        );
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WhenPresent_ReturnsTheCount()
    {
        SetupDatabase();
        MockDatabase
            .Setup(x => x.StringGetAsync(KeyPrefix + "key", CommandFlags.None))
            .ReturnsAsync(7)
            .Verifiable();

        var count = await CounterSet.GetAsync("key", CancellationToken.None);

        Assert.Equal(7, count);
        AssertOperationRecorded(ValkeySharedStateMetrics.CounterGetOperation, errorType: null);
    }

    [Fact]
    public async Task GetAsync_WhenAbsent_ReturnsZero()
    {
        SetupDatabase();
        MockDatabase
            .Setup(x => x.StringGetAsync(KeyPrefix + "key", CommandFlags.None))
            .ReturnsAsync(RedisValue.Null)
            .Verifiable();

        var count = await CounterSet.GetAsync("key", CancellationToken.None);

        Assert.Equal(0, count);
    }

    #endregion
}
