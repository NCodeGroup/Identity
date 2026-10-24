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

public sealed class ValkeyReplicaBroadcasterTests : BaseTests
{
    private const string KeyPrefix = "test:";

    private Mock<IValkeyConnection> MockConnection { get; }

    private Mock<ISubscriber> MockSubscriber { get; }

    private ValkeyReplicaBroadcaster Broadcaster { get; }

    private ServiceProvider MetricsProvider { get; }

    private MetricCollector<double> DurationCollector { get; }

    public ValkeyReplicaBroadcasterTests()
    {
        MockConnection = CreateStrictMock<IValkeyConnection>();
        MockSubscriber = CreateStrictMock<ISubscriber>();

        MetricsProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = MetricsProvider.GetRequiredService<IMeterFactory>();
        DurationCollector = new MetricCollector<double>(
            meterFactory,
            ValkeyMetrics.MeterName,
            ValkeySharedStateMetrics.OperationDurationName
        );

        Broadcaster = new ValkeyReplicaBroadcaster(
            MockConnection.Object,
            Microsoft.Extensions.Options.Options.Create(
                new ValkeyOptions { KeyPrefix = KeyPrefix }
            ),
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

    #region PublishAsync Tests

    [Fact]
    public async Task PublishAsync_PublishesTheMessageOnThePrefixedChannel()
    {
        MockConnection
            .Setup(x => x.GetSubscriberAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(MockSubscriber.Object)
            .Verifiable();
        MockSubscriber
            .Setup(x =>
                x.PublishAsync(
                    RedisChannel.Literal("test:tenant-changed"),
                    "acme",
                    CommandFlags.None
                )
            )
            .ReturnsAsync(1L)
            .Verifiable();

        await Broadcaster.PublishAsync("tenant-changed", "acme", CancellationToken.None);

        AssertOperationRecorded(
            ValkeySharedStateMetrics.BroadcastPublishOperation,
            errorType: null
        );
    }

    [Fact]
    public async Task PublishAsync_WhenTheServerIsUnreachable_ThrowsAndRecordsTheError()
    {
        MockConnection
            .Setup(x => x.GetSubscriberAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(
                new RedisConnectionException(
                    ConnectionFailureType.UnableToConnect,
                    CommandFlags.None,
                    "down"
                )
            )
            .Verifiable();

        await Assert.ThrowsAsync<RedisConnectionException>(async () =>
            await Broadcaster.PublishAsync("tenant-changed", "acme", CancellationToken.None)
        );

        AssertOperationRecorded(
            ValkeySharedStateMetrics.BroadcastPublishOperation,
            typeof(RedisConnectionException).FullName
        );
    }

    #endregion
}
