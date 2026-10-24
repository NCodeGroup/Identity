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

using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NCode.Identity.SharedState.Valkey.Facades;
using NCode.Identity.SharedState.Valkey.Options;
using StackExchange.Redis;
using Xunit;

namespace NCode.Identity.SharedState.Valkey.Tests;

public sealed class ValkeyReplicaBroadcastSubscriberTests : BaseTests
{
    private const string KeyPrefix = "test:";
    private const string Channel = "tenant-changed";
    private static readonly RedisChannel PrefixedChannel = RedisChannel.Literal(
        KeyPrefix + Channel
    );

    private static TimeSpan Timeout { get; } = TimeSpan.FromSeconds(5);

    private Mock<IValkeyConnection> MockConnection { get; }

    private Mock<ISubscriber> MockSubscriber { get; }

    private Mock<IConnectionMultiplexer> MockMultiplexer { get; }

    private EventHandler<ConnectionFailedEventArgs>? Restored { get; set; }

    public ValkeyReplicaBroadcastSubscriberTests()
    {
        MockConnection = CreateStrictMock<IValkeyConnection>();
        MockSubscriber = CreateStrictMock<ISubscriber>();
        MockMultiplexer = CreateStrictMock<IConnectionMultiplexer>();
    }

    private void SetupMultiplexer()
    {
        MockSubscriber.Setup(x => x.Multiplexer).Returns(MockMultiplexer.Object).Verifiable();
        MockMultiplexer
            .SetupAdd(x =>
                x.ConnectionRestored += It.IsAny<EventHandler<ConnectionFailedEventArgs>>()
            )
            .Callback<EventHandler<ConnectionFailedEventArgs>>(handler => Restored = handler)
            .Verifiable();
        MockMultiplexer
            .SetupRemove(x =>
                x.ConnectionRestored -= It.IsAny<EventHandler<ConnectionFailedEventArgs>>()
            )
            .Verifiable();
    }

    private Mock<IReplicaBroadcastHandler> CreateHandler(string channel)
    {
        var mockHandler = CreateStrictMock<IReplicaBroadcastHandler>();
        mockHandler.Setup(x => x.Channel).Returns(channel).Verifiable();
        return mockHandler;
    }

    private static void SetupMissedMessages(Mock<IReplicaBroadcastHandler> mockHandler) =>
        mockHandler
            .Setup(x => x.HandleMissedMessagesAsync(CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

    private ValkeyReplicaBroadcastSubscriber CreateSubscriber(
        TimeSpan retryDelay,
        params IReplicaBroadcastHandler[] handlers
    ) =>
        new(
            MockConnection.Object,
            Microsoft.Extensions.Options.Options.Create(
                new ValkeyOptions { KeyPrefix = KeyPrefix }
            ),
            handlers,
            NullLogger<ValkeyReplicaBroadcastSubscriber>.Instance
        )
        {
            RetryDelay = retryDelay,
        };

    private TaskCompletionSource<Action<RedisChannel, RedisValue>> SetupSubscribe()
    {
        var subscribed = new TaskCompletionSource<Action<RedisChannel, RedisValue>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        MockSubscriber
            .Setup(x =>
                x.SubscribeAsync(
                    PrefixedChannel,
                    It.IsAny<Action<RedisChannel, RedisValue>>(),
                    CommandFlags.None
                )
            )
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) => subscribed.TrySetResult(handler)
            )
            .Returns(Task.CompletedTask)
            .Verifiable();
        return subscribed;
    }

    #region ExecuteAsync Tests

    [Fact]
    public async Task ExecuteAsync_DeliversAMessageToTheHandlersOfItsChannel()
    {
        var handled = new[]
        {
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously),
        };
        var mockFirst = CreateHandler(Channel);
        mockFirst
            .Setup(x => x.HandleAsync("acme", CancellationToken.None))
            .Callback(() => handled[0].TrySetResult())
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupMissedMessages(mockFirst);
        var mockSecond = CreateHandler(Channel);
        mockSecond
            .Setup(x => x.HandleAsync("acme", CancellationToken.None))
            .Callback(() => handled[1].TrySetResult())
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        SetupMissedMessages(mockSecond);
        MockConnection
            .Setup(x => x.GetSubscriberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockSubscriber.Object)
            .Verifiable();
        var subscribed = SetupSubscribe();
        SetupMultiplexer();

        using var subscriber = CreateSubscriber(
            TimeSpan.FromSeconds(5),
            mockFirst.Object,
            mockSecond.Object
        );
        await subscriber.StartAsync(CancellationToken.None);
        var onMessage = await subscribed.Task.WaitAsync(Timeout);

        onMessage(PrefixedChannel, "acme");
        await Task.WhenAll(handled.Select(x => x.Task)).WaitAsync(Timeout);

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheServerIsUnreachable_RetriesTheSubscription()
    {
        var attempts = 0;
        MockConnection
            .Setup(x => x.GetSubscriberAsync(It.IsAny<CancellationToken>()))
            .Returns(() =>
                ++attempts == 1
                    ? ValueTask.FromException<ISubscriber>(
                        new RedisConnectionException(
                            ConnectionFailureType.UnableToConnect,
                            CommandFlags.None,
                            "down"
                        )
                    )
                    : ValueTask.FromResult(MockSubscriber.Object)
            )
            .Verifiable();
        var subscribed = SetupSubscribe();
        SetupMultiplexer();
        var mockHandler = CreateHandler(Channel);
        SetupMissedMessages(mockHandler);

        using var subscriber = CreateSubscriber(TimeSpan.FromMilliseconds(10), mockHandler.Object);
        await subscriber.StartAsync(CancellationToken.None);
        await subscribed.Task.WaitAsync(Timeout);

        Assert.Equal(2, attempts);

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenSubscribingFails_RemovesTheHandlerBeforeRetrying()
    {
        MockConnection
            .Setup(x => x.GetSubscriberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockSubscriber.Object)
            .Verifiable();
        var calls = 0;
        var subscribed = new List<Action<RedisChannel, RedisValue>>();
        var unsubscribed = new List<Action<RedisChannel, RedisValue>?>();
        var secondCall = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        MockSubscriber
            .Setup(x =>
                x.SubscribeAsync(
                    PrefixedChannel,
                    It.IsAny<Action<RedisChannel, RedisValue>>(),
                    CommandFlags.None
                )
            )
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>, CommandFlags>(
                (_, handler, _) =>
                {
                    subscribed.Add(handler);
                    if (subscribed.Count == 2)
                        secondCall.TrySetResult();
                }
            )
            .Returns(() =>
                ++calls == 1
                    ? Task.FromException(
                        new RedisConnectionException(
                            ConnectionFailureType.UnableToConnect,
                            CommandFlags.None,
                            "down"
                        )
                    )
                    : Task.CompletedTask
            )
            .Verifiable();
        MockSubscriber
            .Setup(x =>
                x.Unsubscribe(
                    PrefixedChannel,
                    It.IsAny<Action<RedisChannel, RedisValue>>(),
                    CommandFlags.FireAndForget
                )
            )
            .Callback<RedisChannel, Action<RedisChannel, RedisValue>?, CommandFlags>(
                (_, handler, _) => unsubscribed.Add(handler)
            )
            .Verifiable();
        SetupMultiplexer();
        var mockHandler = CreateHandler(Channel);
        SetupMissedMessages(mockHandler);

        using var subscriber = CreateSubscriber(TimeSpan.FromMilliseconds(10), mockHandler.Object);
        await subscriber.StartAsync(CancellationToken.None);
        await secondCall.Task.WaitAsync(Timeout);

        Assert.Equal(2, subscribed.Count);
        Assert.Same(subscribed[0], Assert.Single(unsubscribed));

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_AfterSubscribingAChannel_TellsItsHandlersTheyMayHaveMissedMessages()
    {
        var told = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var mockHandler = CreateHandler(Channel);
        mockHandler
            .Setup(x => x.HandleMissedMessagesAsync(CancellationToken.None))
            .Callback(() => told.TrySetResult())
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockConnection
            .Setup(x => x.GetSubscriberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockSubscriber.Object)
            .Verifiable();
        SetupSubscribe();
        SetupMultiplexer();

        using var subscriber = CreateSubscriber(TimeSpan.FromSeconds(5), mockHandler.Object);
        await subscriber.StartAsync(CancellationToken.None);
        await told.Task.WaitAsync(Timeout);

        await subscriber.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTheConnectionIsRestored_TellsEveryHandlerItMayHaveMissedMessages()
    {
        var calls = 0;
        var toldTwice = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        var mockHandler = CreateHandler(Channel);
        mockHandler
            .Setup(x => x.HandleMissedMessagesAsync(CancellationToken.None))
            .Callback(() =>
            {
                if (Interlocked.Increment(ref calls) == 2)
                    toldTwice.TrySetResult();
            })
            .Returns(ValueTask.CompletedTask)
            .Verifiable();
        MockConnection
            .Setup(x => x.GetSubscriberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockSubscriber.Object)
            .Verifiable();
        var subscribed = SetupSubscribe();
        SetupMultiplexer();

        using var subscriber = CreateSubscriber(TimeSpan.FromSeconds(5), mockHandler.Object);
        await subscriber.StartAsync(CancellationToken.None);
        await subscribed.Task.WaitAsync(Timeout);

        Assert.NotNull(Restored);
        Restored(
            this,
            new ConnectionFailedEventArgs(
                this,
                new DnsEndPoint("localhost", 6379),
                ConnectionType.Subscription,
                ConnectionFailureType.None,
                new InvalidOperationException("restored"),
                "physical"
            )
        );
        await toldTwice.Task.WaitAsync(Timeout);

        await subscriber.StopAsync(CancellationToken.None);
    }

    #endregion

    #region DispatchAsync Tests

    [Fact]
    public async Task DispatchAsync_WhenAHandlerFails_DeliversToTheOthers()
    {
        var mockFailing = CreateHandler(Channel);
        mockFailing
            .Setup(x => x.HandleAsync("acme", CancellationToken.None))
            .Returns(() => ValueTask.FromException(new InvalidOperationException("boom")))
            .Verifiable();
        var mockOther = CreateHandler("other-channel");
        var mockHealthy = CreateHandler(Channel);
        mockHealthy
            .Setup(x => x.HandleAsync("acme", CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        using var subscriber = CreateSubscriber(
            TimeSpan.FromSeconds(5),
            mockFailing.Object,
            mockOther.Object,
            mockHealthy.Object
        );

        await subscriber.DispatchAsync(Channel, "acme");

        mockOther.Verify(
            x => x.HandleAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never
        );
    }

    #endregion
}
