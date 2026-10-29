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

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Moq;
using NCode.Identity.Bff.Diagnostics;
using NCode.Identity.Bff.Sessions;
using Xunit;

namespace NCode.Identity.Bff.Tests.Sessions;

public sealed class BffInstrumentedSessionStoreTests : BaseTests
{
    private Mock<IBffSessionStore> MockStore { get; }
    private ServiceProvider MetricsProvider { get; }
    private MetricCollector<double> DurationCollector { get; }
    private ActivityListener Listener { get; }
    private List<Activity> StoppedActivities { get; } = [];
    private Activity Parent { get; }
    private BffInstrumentedSessionStore Store { get; }

    public BffInstrumentedSessionStoreTests()
    {
        MockStore = CreateStrictMock<IBffSessionStore>();
        MetricsProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = MetricsProvider.GetRequiredService<IMeterFactory>();
        DurationCollector = new MetricCollector<double>(
            meterFactory,
            BffTelemetry.MeterName,
            BffMetrics.SessionStoreOperationDurationName
        );

        // The activity source is shared by every test, so only the spans under this test's parent are kept.
        Parent = new Activity(nameof(BffInstrumentedSessionStoreTests)).Start();
        var parentSpanId = Parent.SpanId;
        Listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == BffTelemetry.ActivitySourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity =>
            {
                if (activity.ParentSpanId != parentSpanId)
                    return;

                lock (StoppedActivities)
                    StoppedActivities.Add(activity);
            },
        };
        ActivitySource.AddActivityListener(Listener);

        Store = new BffInstrumentedSessionStore(MockStore.Object, new BffMetrics(meterFactory));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Listener.Dispose();
            Parent.Dispose();
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
        Assert.Equal(operation, measurement.Tags[BffMetrics.SessionStoreOperationAttribute]);
        if (errorType is null)
            Assert.False(measurement.Tags.ContainsKey(BffMetrics.ErrorTypeAttribute));
        else
            Assert.Equal(errorType, measurement.Tags[BffMetrics.ErrorTypeAttribute]);

        var activity = Assert.Single(StoppedActivities);
        Assert.Equal(BffActivities.SessionStoreOperation, activity.OperationName);
        Assert.Equal(operation, activity.GetTagItem(BffMetrics.SessionStoreOperationAttribute));
        Assert.Equal(errorType, activity.GetTagItem(BffMetrics.ErrorTypeAttribute));
        Assert.Equal(
            errorType is null ? ActivityStatusCode.Unset : ActivityStatusCode.Error,
            activity.Status
        );
    }

    #region Operation Tests

    [Fact]
    public async Task GetAsync_WhenTheStoreAnswers_ReturnsItsAnswerAndRecordsTheOperation()
    {
        MockStore
            .Setup(x => x.GetAsync("store-key", It.IsAny<CancellationToken>()))
            .ReturnsAsync((BffSessionRecord?)null)
            .Verifiable();

        var record = await Store.GetAsync("store-key", CancellationToken.None);

        Assert.Null(record);
        AssertOperationRecorded(BffMetrics.GetOperation, errorType: null);
    }

    [Fact]
    public async Task ReleaseRefreshLeaseAsync_WhenTheStoreCompletes_RecordsTheOperation()
    {
        MockStore
            .Setup(x => x.ReleaseRefreshLeaseAsync("lease-key", It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Store.ReleaseRefreshLeaseAsync("lease-key", CancellationToken.None);

        AssertOperationRecorded(BffMetrics.ReleaseRefreshLeaseOperation, errorType: null);
    }

    [Fact]
    public async Task TryAcquireRefreshLeaseAsync_WhenTheStoreThrows_RecordsTheErrorAndRethrows()
    {
        var expiresWhen = new DateTimeOffset(2026, 1, 2, 5, 0, 0, TimeSpan.Zero);
        var failure = new InvalidOperationException("The store is down.");
        MockStore
            .Setup(x =>
                x.TryAcquireRefreshLeaseAsync(
                    "lease-key",
                    expiresWhen,
                    It.IsAny<CancellationToken>()
                )
            )
            .ThrowsAsync(failure)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.TryAcquireRefreshLeaseAsync(
                "lease-key",
                expiresWhen,
                CancellationToken.None
            )
        );

        Assert.Same(failure, exception);
        AssertOperationRecorded(
            BffMetrics.AcquireRefreshLeaseOperation,
            typeof(InvalidOperationException).FullName
        );
    }

    [Fact]
    public async Task RemoveAsync_WhenTheStoreThrows_RecordsTheErrorAndRethrows()
    {
        var metadata = new BffSessionMetadata
        {
            Frontend = BffDefaults.FrontendName,
            Issuer = "https://op.example",
            Subject = "subject",
            CreatedAt = DateTimeOffset.UnixEpoch,
            LastAccessedAt = DateTimeOffset.UnixEpoch,
            AbsoluteExpiresAt = DateTimeOffset.UnixEpoch,
            IdleExpiresAt = DateTimeOffset.UnixEpoch,
        };
        MockStore
            .Setup(x => x.RemoveAsync("store-key", metadata, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new TimeoutException())
            .Verifiable();

        await Assert.ThrowsAsync<TimeoutException>(async () =>
            await Store.RemoveAsync("store-key", metadata, CancellationToken.None)
        );

        AssertOperationRecorded(BffMetrics.RemoveOperation, typeof(TimeoutException).FullName);
    }

    #endregion
}
