// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Moq;
using NCode.Identity.Bff.Diagnostics;
using NCode.Identity.Bff.Sessions;
using NCode.Identity.Bff.Tests.Frontends;
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
    public async Task FindBySubjectAsync_WhenTheStoreAnswers_ReturnsItsAnswerAndRecordsTheOperation()
    {
        IReadOnlyList<string> keys = ["store-key"];
        MockStore
            .Setup(x =>
                x.FindBySubjectAsync("https://op.example", "subject", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(keys)
            .Verifiable();

        var result = await Store.FindBySubjectAsync(
            "https://op.example",
            "subject",
            CancellationToken.None
        );

        Assert.Same(keys, result);
        AssertOperationRecorded(BffMetrics.FindBySubjectOperation, errorType: null);
    }

    [Fact]
    public async Task FindClientsAsync_WhenTheStoreAnswers_ReturnsItsAnswerAndRecordsTheOperation()
    {
        IReadOnlyList<BffSessionClient> clients =
        [
            new BffSessionClient(BffTestFrontends.Name, "https://op.example", "client-1"),
        ];
        MockStore
            .Setup(x => x.FindClientsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(clients)
            .Verifiable();

        var result = await Store.FindClientsAsync(CancellationToken.None);

        Assert.Same(clients, result);
        AssertOperationRecorded(BffMetrics.FindClientsOperation, errorType: null);
    }

    [Fact]
    public async Task FindByClientAsync_WhenTheStoreAnswers_ReturnsItsAnswerAndRecordsTheOperation()
    {
        var client = new BffSessionClient(BffTestFrontends.Name, "https://op.example", "client-1");
        IReadOnlyList<string> keys = ["store-key"];
        MockStore
            .Setup(x => x.FindByClientAsync(client, It.IsAny<CancellationToken>()))
            .ReturnsAsync(keys)
            .Verifiable();

        var result = await Store.FindByClientAsync(client, CancellationToken.None);

        Assert.Same(keys, result);
        AssertOperationRecorded(BffMetrics.FindByClientOperation, errorType: null);
    }

    [Fact]
    public async Task FindByClientAsync_WhenTheStoreThrows_RecordsTheErrorAndRethrows()
    {
        var client = new BffSessionClient(BffTestFrontends.Name, "https://op.example", "client-1");
        var failure = new InvalidOperationException("The store is down.");
        MockStore
            .Setup(x => x.FindByClientAsync(client, It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure)
            .Verifiable();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Store.FindByClientAsync(client, CancellationToken.None)
        );

        Assert.Same(failure, exception);
        AssertOperationRecorded(
            BffMetrics.FindByClientOperation,
            typeof(InvalidOperationException).FullName
        );
    }

    [Fact]
    public async Task ReleaseRefreshLeaseAsync_WhenTheStoreCompletes_RecordsTheOperation()
    {
        var lease = new BffRefreshLease("lease-key", "owner-1");
        MockStore
            .Setup(x => x.ReleaseRefreshLeaseAsync(lease, It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Store.ReleaseRefreshLeaseAsync(lease, CancellationToken.None);

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
            Frontend = BffTestFrontends.Name,
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
