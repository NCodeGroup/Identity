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

    public ValkeyExpiringCounterSetTests()
    {
        MockConnection = CreateStrictMock<IValkeyConnection>();
        MockDatabase = CreateStrictMock<IDatabase>();
        MockTimeProvider = CreateStrictMock<TimeProvider>();

        CounterSet = new ValkeyExpiringCounterSet(
            MockConnection.Object,
            Microsoft.Extensions.Options.Options.Create(
                new ValkeyOptions { KeyPrefix = KeyPrefix }
            ),
            MockTimeProvider.Object
        );
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
