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

public sealed class ValkeyExpiringValueSetTests : BaseTests
{
    private const string KeyPrefix = "test:";

    private static DateTimeOffset Now { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static byte[] Payload { get; } = [1, 2, 3];

    private Mock<IValkeyConnection> MockConnection { get; }

    private Mock<IDatabase> MockDatabase { get; }

    private Mock<TimeProvider> MockTimeProvider { get; }

    private ValkeyExpiringValueSet ValueSet { get; }

    private ServiceProvider MetricsProvider { get; }

    private MetricCollector<double> DurationCollector { get; }

    public ValkeyExpiringValueSetTests()
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

        ValueSet = new ValkeyExpiringValueSet(
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

    private void SetupSet(TimeSpan timeToLive, bool result)
    {
        RedisValue expectedValue = ValkeyExpiringValueSet.Encode(1, Payload);

        MockDatabase
            .Setup(x =>
                x.StringSetAsync(
                    KeyPrefix + "key",
                    expectedValue,
                    new Expiration(timeToLive),
                    ValueCondition.NotExists,
                    CommandFlags.None
                )
            )
            .ReturnsAsync(result)
            .Verifiable();
    }

    private void SetupReplace(long expectedVersion, long milliseconds, RedisResult result)
    {
        RedisValue[] expectedValues =
        [
            ValkeyExpiringValueSet.Encode(expectedVersion, []),
            ValkeyExpiringValueSet.Encode(expectedVersion + 1, Payload),
            milliseconds,
        ];

        MockDatabase
            .Setup(x =>
                x.ScriptEvaluateAsync(
                    ValkeyExpiringValueSet.ReplaceScript,
                    It.Is<RedisKey[]>(keys =>
                        keys.SequenceEqual(new RedisKey[] { KeyPrefix + "key" })
                    ),
                    It.Is<RedisValue[]>(values => values.SequenceEqual(expectedValues)),
                    CommandFlags.None
                )
            )
            .ReturnsAsync(result)
            .Verifiable();
    }

    private static RedisConnectionException CreateDownException() =>
        new(ConnectionFailureType.UnableToConnect, CommandFlags.None, "down");

    #region Encode and Decode Tests

    [Fact]
    public void Decode_OfEncode_RoundTripsValueAndVersion()
    {
        var stored = ValkeyExpiringValueSet.Encode(0x0102030405060708, Payload);

        var result = ValkeyExpiringValueSet.Decode(stored);

        Assert.Equal(0x0102030405060708, result.Version);
        Assert.Equal(Payload, result.Value.ToArray());
        Assert.Equal(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 }, stored.Take(8).ToArray());
    }

    #endregion

    #region TryAddAsync Tests

    [Fact]
    public async Task TryAddAsync_StoresVersionOneWithTheExpiryIfAbsent()
    {
        SetupNow();
        SetupDatabase();
        SetupSet(TimeSpan.FromSeconds(30), result: true);

        var added = await ValueSet.TryAddAsync(
            "key",
            Payload,
            Now.AddSeconds(30),
            CancellationToken.None
        );

        Assert.True(added);
        AssertOperationRecorded(ValkeySharedStateMetrics.ValueSetTryAddOperation, errorType: null);
    }

    [Fact]
    public async Task TryAddAsync_WhenExpiryNotInFuture_ReturnsFalseWithoutACommand()
    {
        SetupNow();

        var added = await ValueSet.TryAddAsync("key", Payload, Now, CancellationToken.None);

        Assert.False(added);
        Assert.Empty(DurationCollector.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task TryAddAsync_WhenCommandThrows_RecordsTheErrorType()
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
            .ThrowsAsync(CreateDownException())
            .Verifiable();

        await Assert.ThrowsAsync<RedisConnectionException>(async () =>
            await ValueSet.TryAddAsync("key", Payload, Now.AddSeconds(30), CancellationToken.None)
        );
        AssertOperationRecorded(
            ValkeySharedStateMetrics.ValueSetTryAddOperation,
            typeof(RedisConnectionException).FullName
        );
    }

    #endregion

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WhenPresent_ReturnsTheValueAndVersion()
    {
        SetupDatabase();
        MockDatabase
            .Setup(x => x.StringGetAsync(KeyPrefix + "key", CommandFlags.None))
            .ReturnsAsync((RedisValue)ValkeyExpiringValueSet.Encode(4, Payload))
            .Verifiable();

        var result = await ValueSet.GetAsync("key", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(4, result.Value.Version);
        Assert.Equal(Payload, result.Value.Value.ToArray());
        AssertOperationRecorded(ValkeySharedStateMetrics.ValueSetGetOperation, errorType: null);
    }

    [Fact]
    public async Task GetAsync_WhenAbsent_ReturnsNull()
    {
        SetupDatabase();
        MockDatabase
            .Setup(x => x.StringGetAsync(KeyPrefix + "key", CommandFlags.None))
            .ReturnsAsync(RedisValue.Null)
            .Verifiable();

        var result = await ValueSet.GetAsync("key", CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetAsync_WhenStoredValueIsShorterThanItsVersion_Throws()
    {
        SetupDatabase();
        MockDatabase
            .Setup(x => x.StringGetAsync(KeyPrefix + "key", CommandFlags.None))
            .ReturnsAsync((RedisValue)new byte[] { 1, 2, 3 })
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await ValueSet.GetAsync("key", CancellationToken.None)
        );
    }

    #endregion

    #region TryReplaceAsync Tests

    [Fact]
    public async Task TryReplaceAsync_RunsTheScriptWithTheVersionsAndReturnsTrue()
    {
        SetupNow();
        SetupDatabase();
        SetupReplace(3, 30_000, RedisResult.Create(1L));

        var replaced = await ValueSet.TryReplaceAsync(
            "key",
            3,
            Payload,
            Now.AddSeconds(30),
            CancellationToken.None
        );

        Assert.True(replaced);
        AssertOperationRecorded(
            ValkeySharedStateMetrics.ValueSetTryReplaceOperation,
            errorType: null
        );
    }

    [Fact]
    public async Task TryReplaceAsync_WhenScriptReturnsZero_ReturnsFalse()
    {
        SetupNow();
        SetupDatabase();
        SetupReplace(3, 30_000, RedisResult.Create(0L));

        var replaced = await ValueSet.TryReplaceAsync(
            "key",
            3,
            Payload,
            Now.AddSeconds(30),
            CancellationToken.None
        );

        Assert.False(replaced);
    }

    [Fact]
    public async Task TryReplaceAsync_WhenExpiryNotInFuture_ReturnsFalseWithoutACommand()
    {
        SetupNow();

        var replaced = await ValueSet.TryReplaceAsync(
            "key",
            3,
            Payload,
            Now,
            CancellationToken.None
        );

        Assert.False(replaced);
        Assert.Empty(DurationCollector.GetMeasurementSnapshot());
    }

    #endregion

    #region TryTakeAsync Tests

    [Fact]
    public async Task TryTakeAsync_WhenPresent_ReturnsTheValueAndVersion()
    {
        SetupDatabase();
        MockDatabase
            .Setup(x => x.StringGetDeleteAsync(KeyPrefix + "key", CommandFlags.None))
            .ReturnsAsync((RedisValue)ValkeyExpiringValueSet.Encode(2, Payload))
            .Verifiable();

        var result = await ValueSet.TryTakeAsync("key", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal(2, result.Value.Version);
        Assert.Equal(Payload, result.Value.Value.ToArray());
        AssertOperationRecorded(ValkeySharedStateMetrics.ValueSetTryTakeOperation, errorType: null);
    }

    [Fact]
    public async Task TryTakeAsync_WhenAbsent_ReturnsNull()
    {
        SetupDatabase();
        MockDatabase
            .Setup(x => x.StringGetDeleteAsync(KeyPrefix + "key", CommandFlags.None))
            .ReturnsAsync(RedisValue.Null)
            .Verifiable();

        var result = await ValueSet.TryTakeAsync("key", CancellationToken.None);

        Assert.Null(result);
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_DeletesThePrefixedKey()
    {
        SetupDatabase();
        MockDatabase
            .Setup(x => x.KeyDeleteAsync(KeyPrefix + "key", CommandFlags.None))
            .ReturnsAsync(true)
            .Verifiable();

        await ValueSet.RemoveAsync("key", CancellationToken.None);

        AssertOperationRecorded(ValkeySharedStateMetrics.ValueSetRemoveOperation, errorType: null);
    }

    #endregion
}
