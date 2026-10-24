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

public sealed class ValkeyExpiringKeySetTests : BaseTests
{
    private const string KeyPrefix = "test:";

    private static DateTimeOffset Now { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private Mock<IValkeyConnection> MockConnection { get; }

    private Mock<IDatabase> MockDatabase { get; }

    private Mock<TimeProvider> MockTimeProvider { get; }

    private ValkeyExpiringKeySet KeySet { get; }

    private ServiceProvider MetricsProvider { get; }

    private MetricCollector<double> DurationCollector { get; }

    public ValkeyExpiringKeySetTests()
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

        KeySet = new ValkeyExpiringKeySet(
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

    private void SetupSet(TimeSpan timeToLive, bool result) =>
        MockDatabase
            .Setup(x =>
                x.StringSetAsync(
                    KeyPrefix + "key",
                    1,
                    new Expiration(timeToLive),
                    ValueCondition.NotExists,
                    CommandFlags.None
                )
            )
            .ReturnsAsync(result)
            .Verifiable();

    #region TryAddAsync Tests

    [Fact]
    public async Task TryAddAsync_WhenServerSetsTheKey_ReturnsTrue()
    {
        SetupNow();
        SetupDatabase();
        SetupSet(TimeSpan.FromSeconds(30), result: true);

        var added = await KeySet.TryAddAsync("key", Now.AddSeconds(30), CancellationToken.None);

        Assert.True(added);
        AssertOperationRecorded(ValkeySharedStateMetrics.KeySetTryAddOperation, errorType: null);
    }

    [Fact]
    public async Task TryAddAsync_WhenKeyAlreadyPresent_ReturnsFalse()
    {
        SetupNow();
        SetupDatabase();
        SetupSet(TimeSpan.FromSeconds(30), result: false);

        var added = await KeySet.TryAddAsync("key", Now.AddSeconds(30), CancellationToken.None);

        Assert.False(added);
    }

    [Fact]
    public async Task TryAddAsync_WhenTimeToLiveHasAFractionalMillisecond_RoundsItUp()
    {
        SetupNow();
        SetupDatabase();
        SetupSet(TimeSpan.FromMilliseconds(1), result: true);

        var added = await KeySet.TryAddAsync(
            "key",
            Now.AddTicks(TimeSpan.TicksPerMillisecond / 2),
            CancellationToken.None
        );

        Assert.True(added);
    }

    [Fact]
    public async Task TryAddAsync_WhenExpiryNotInFuture_ReturnsFalseWithoutACommand()
    {
        SetupNow();

        var added = await KeySet.TryAddAsync("key", Now, CancellationToken.None);

        Assert.False(added);
        Assert.Empty(DurationCollector.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task TryAddAsync_WhenServerUnreachable_Throws()
    {
        SetupNow();
        SetupDatabase();
        MockDatabase
            .Setup(x =>
                x.StringSetAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<RedisValue>(),
                    It.IsAny<Expiration>(),
                    It.IsAny<ValueCondition>(),
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
            await KeySet.TryAddAsync("key", Now.AddSeconds(30), CancellationToken.None)
        );
        AssertOperationRecorded(
            ValkeySharedStateMetrics.KeySetTryAddOperation,
            typeof(RedisConnectionException).FullName
        );
    }

    #endregion

    #region ContainsAsync Tests

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ContainsAsync_ReturnsWhetherThePrefixedKeyExists(bool exists)
    {
        SetupDatabase();
        MockDatabase
            .Setup(x => x.KeyExistsAsync(KeyPrefix + "key", CommandFlags.None))
            .ReturnsAsync(exists)
            .Verifiable();

        var contains = await KeySet.ContainsAsync("key", CancellationToken.None);

        Assert.Equal(exists, contains);
        AssertOperationRecorded(ValkeySharedStateMetrics.KeySetContainsOperation, errorType: null);
    }

    #endregion
}
