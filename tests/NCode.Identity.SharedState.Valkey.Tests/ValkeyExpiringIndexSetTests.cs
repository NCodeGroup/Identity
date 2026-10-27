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

public sealed class ValkeyExpiringIndexSetTests : BaseTests
{
    private const string KeyPrefix = "test:";

    private static DateTimeOffset Now { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private Mock<IValkeyConnection> MockConnection { get; }

    private Mock<IDatabase> MockDatabase { get; }

    private Mock<TimeProvider> MockTimeProvider { get; }

    private ValkeyExpiringIndexSet IndexSet { get; }

    private ServiceProvider MetricsProvider { get; }

    private MetricCollector<double> DurationCollector { get; }

    public ValkeyExpiringIndexSetTests()
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

        IndexSet = new ValkeyExpiringIndexSet(
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

    private static RedisConnectionException CreateDownException() =>
        new(ConnectionFailureType.UnableToConnect, CommandFlags.None, "down");

    #region AddMemberAsync Tests

    [Fact]
    public async Task AddMemberAsync_RunsTheScriptWithTheScoreAndTimeToLive()
    {
        SetupNow();
        SetupDatabase();
        RedisValue[] expectedValues =
        [
            Now.ToUnixTimeMilliseconds(),
            Now.AddSeconds(30).ToUnixTimeMilliseconds(),
            "member",
            30_000L,
        ];
        MockDatabase
            .Setup(x =>
                x.ScriptEvaluateAsync(
                    ValkeyExpiringIndexSet.AddMemberScript,
                    It.Is<RedisKey[]>(keys =>
                        keys.SequenceEqual(new RedisKey[] { KeyPrefix + "index" })
                    ),
                    It.Is<RedisValue[]>(values => values.SequenceEqual(expectedValues)),
                    CommandFlags.None
                )
            )
            .ReturnsAsync(RedisResult.Create(1L))
            .Verifiable();

        await IndexSet.AddMemberAsync(
            "index",
            "member",
            Now.AddSeconds(30),
            CancellationToken.None
        );

        AssertOperationRecorded(
            ValkeySharedStateMetrics.IndexSetAddMemberOperation,
            errorType: null
        );
    }

    [Fact]
    public async Task AddMemberAsync_WhenExpiryNotInFuture_IssuesNoCommand()
    {
        SetupNow();

        await IndexSet.AddMemberAsync("index", "member", Now, CancellationToken.None);

        Assert.Empty(DurationCollector.GetMeasurementSnapshot());
    }

    [Fact]
    public async Task AddMemberAsync_WhenCommandThrows_RecordsTheErrorType()
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
            .ThrowsAsync(CreateDownException())
            .Verifiable();

        await Assert.ThrowsAsync<RedisConnectionException>(async () =>
            await IndexSet.AddMemberAsync(
                "index",
                "member",
                Now.AddSeconds(30),
                CancellationToken.None
            )
        );
        AssertOperationRecorded(
            ValkeySharedStateMetrics.IndexSetAddMemberOperation,
            typeof(RedisConnectionException).FullName
        );
    }

    #endregion

    #region GetMembersAsync Tests

    [Fact]
    public async Task GetMembersAsync_ReturnsTheUnexpiredMembers()
    {
        SetupNow();
        SetupDatabase();
        double start = Now.ToUnixTimeMilliseconds();
        MockDatabase
            .Setup(x =>
                x.SortedSetRangeByScoreAsync(
                    KeyPrefix + "index",
                    start,
                    double.PositiveInfinity,
                    Exclude.Start,
                    Order.Ascending,
                    0,
                    -1,
                    CommandFlags.None
                )
            )
            .ReturnsAsync(new RedisValue[] { "a", "b" })
            .Verifiable();

        var members = await IndexSet.GetMembersAsync("index", CancellationToken.None);

        Assert.Equal(new[] { "a", "b" }, members);
        AssertOperationRecorded(
            ValkeySharedStateMetrics.IndexSetGetMembersOperation,
            errorType: null
        );
    }

    [Fact]
    public async Task GetMembersAsync_WhenIndexAbsent_ReturnsEmpty()
    {
        SetupNow();
        SetupDatabase();
        MockDatabase
            .Setup(x =>
                x.SortedSetRangeByScoreAsync(
                    It.IsAny<RedisKey>(),
                    It.IsAny<double>(),
                    It.IsAny<double>(),
                    It.IsAny<Exclude>(),
                    It.IsAny<Order>(),
                    It.IsAny<long>(),
                    It.IsAny<long>(),
                    It.IsAny<CommandFlags>()
                )
            )
            .ReturnsAsync(Array.Empty<RedisValue>())
            .Verifiable();

        var members = await IndexSet.GetMembersAsync("index", CancellationToken.None);

        Assert.Empty(members);
    }

    #endregion

    #region RemoveMemberAsync Tests

    [Fact]
    public async Task RemoveMemberAsync_RemovesTheMemberFromThePrefixedKey()
    {
        SetupDatabase();
        MockDatabase
            .Setup(x => x.SortedSetRemoveAsync(KeyPrefix + "index", "member", CommandFlags.None))
            .ReturnsAsync(true)
            .Verifiable();

        await IndexSet.RemoveMemberAsync("index", "member", CancellationToken.None);

        AssertOperationRecorded(
            ValkeySharedStateMetrics.IndexSetRemoveMemberOperation,
            errorType: null
        );
    }

    #endregion

    #region RemoveAsync Tests

    [Fact]
    public async Task RemoveAsync_DeletesThePrefixedKey()
    {
        SetupDatabase();
        MockDatabase
            .Setup(x => x.KeyDeleteAsync(KeyPrefix + "index", CommandFlags.None))
            .ReturnsAsync(true)
            .Verifiable();

        await IndexSet.RemoveAsync("index", CancellationToken.None);

        AssertOperationRecorded(ValkeySharedStateMetrics.IndexSetRemoveOperation, errorType: null);
    }

    #endregion
}
