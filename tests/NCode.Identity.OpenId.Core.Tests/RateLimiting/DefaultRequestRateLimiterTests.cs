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

using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Metrics;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.OpenId.Authentication.SharedState;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.RateLimiting;

public sealed class DefaultRequestRateLimiterTests : BaseTests
{
    private const string TenantId = "tenant-1";

    private const string Stage = "test";

    private const string PartitionValue = "value";

    // Fifteen seconds into the minute, so the previous window weighs three quarters.
    private static DateTimeOffset WindowStart { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Now { get; } = WindowStart.AddSeconds(15);

    // The one-day window that contains Now starts at midnight UTC.
    private static DateTimeOffset DayStart { get; } = new(2026, 10, 19, 0, 0, 0, TimeSpan.Zero);

    private Mock<IRateLimitDescriptorCatalog> MockCatalog { get; }

    private Mock<IExpiringCounterSet> MockCounterSet { get; }

    private Mock<TimeProvider> MockTimeProvider { get; }

    private Mock<OpenIdContext> MockContext { get; }

    private Mock<IReadOnlySettingCollection> MockTenantSettings { get; }

    private DefaultHttpContext HttpContext { get; } = new();

    private FakeLogger<DefaultRequestRateLimiter> Logger { get; } = new();

    private DefaultRequestRateLimiter Limiter { get; }

    private ServiceProvider MetricsProvider { get; }

    private MetricCollector<long> Collector { get; }

    private List<(string? Partition, string? Result)> Judgements =>
        Collector
            .GetMeasurementSnapshot()
            .Select(measurement =>
                (
                    measurement.Tags.GetValueOrDefault(
                        AuthenticationMetrics.RateLimitingPartitionAttribute
                    ) as string,
                    measurement.Tags.GetValueOrDefault(
                        AuthenticationMetrics.RateLimitingResultAttribute
                    ) as string
                )
            )
            .ToList();

    private List<(int EventId, LogLevel Level)> LogEntries =>
        Logger.Collector.GetSnapshot().Select(record => (record.Id.Id, record.Level)).ToList();

    public DefaultRequestRateLimiterTests()
    {
        // The meter factory scopes the meter to this test, so the collector sees only this limiter's measurements.
        MetricsProvider = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var meterFactory = MetricsProvider.GetRequiredService<IMeterFactory>();
        Collector = new MetricCollector<long>(
            meterFactory,
            OpenIdAuthenticationMetrics.MeterName,
            AuthenticationMetrics.RateLimitingRequestsName
        );

        MockCatalog = CreateStrictMock<IRateLimitDescriptorCatalog>();
        MockCounterSet = CreateStrictMock<IExpiringCounterSet>();
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockContext = CreateStrictMock<OpenIdContext>();
        MockTenantSettings = CreateStrictMock<IReadOnlySettingCollection>();
        Limiter = new DefaultRequestRateLimiter(
            MockCatalog.Object,
            MockCounterSet.Object,
            MockTimeProvider.Object,
            new AuthenticationMetrics(meterFactory),
            Logger
        );

        // Every judgement reads the request's HTTP context for its rate-limit state and its endpoint.
        MockContext.Setup(x => x.Http).Returns(HttpContext).Verifiable();

        // The environment type is read only when a limit applies, so this setup is not verified.
        MockTenantSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.EnvironmentType))
            .Returns(OpenIdConstants.TenantEnvironmentTypes.Production);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            Collector.Dispose();
            MetricsProvider.Dispose();
        }

        base.Dispose(disposing);
    }

    #region Scaffolding

    private void SetupTenant(bool readsSettings = true, bool readsTenantId = true)
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        if (readsTenantId)
            mockTenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        if (readsSettings)
        {
            var mockProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
            mockProvider.Setup(x => x.Collection).Returns(MockTenantSettings.Object).Verifiable();
            mockTenant.Setup(x => x.SettingsProvider).Returns(mockProvider.Object).Verifiable();
        }

        MockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
    }

    private void SetupStage(params RateLimitDescriptor[] descriptors)
    {
        ImmutableArray<RateLimitDescriptor> stage = [.. descriptors];
        MockCatalog.Setup(x => x.GetStage(Stage)).Returns(stage).Verifiable();
    }

    private static void SetupLimit(
        Mock<IReadOnlySettingCollection> mockSettings,
        SettingKey<int> key,
        int? limit
    )
    {
        var value = limit.GetValueOrDefault();
        mockSettings.Setup(x => x.TryGetValue(key, out value)).Returns(limit.HasValue).Verifiable();
    }

    private void SetupIncrements(
        IReadOnlyList<ExpiringCounterIncrement> expected,
        params ExpiringCounterCount[] counts
    )
    {
        IReadOnlyList<ExpiringCounterCount> answer = counts;
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        MockCounterSet
            .Setup(x =>
                x.IncrementUntilExceededAsync(
                    It.Is<IReadOnlyList<ExpiringCounterIncrement>>(actual =>
                        actual.SequenceEqual(expected)
                    ),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(answer)
            .Verifiable();
    }

    private RateLimitContext CreateContext(OpenIdClient? client = null) =>
        new() { OpenIdContext = MockContext.Object, Client = client };

    private static SettingKey<int> GetLimitKey(string name) => new($"{name}_limit");

    private static RateLimitDescriptor CreateDescriptor(
        string name,
        int order = 0,
        bool isMeteredOnly = false,
        string? usageMeter = null,
        Func<RateLimitContext, string?>? selector = null
    ) =>
        new()
        {
            Name = name,
            Stage = Stage,
            Order = order,
            Window = TimeSpan.FromMinutes(1),
            IsSliding = true,
            LimitKey = GetLimitKey(name),
            IsMeteredOnly = isMeteredOnly,
            UsageMeter = usageMeter,
            PartitionValueSelector = selector ?? (static _ => PartitionValue),
        };

    private static ExpiringCounterIncrement CreateSlidingIncrement(string name, long limit) =>
        new()
        {
            Key = CreateKey(name, PartitionValue, WindowStart),
            ExpiresWhen = WindowStart.AddMinutes(2),
            Limit = limit,
            ReferenceKey = CreateKey(name, PartitionValue, WindowStart.AddMinutes(-1)),
            ReferenceWeight = 0.75,
        };

    private static SettingKey<int> GetOverageKey(string name) => new($"{name}_overage");

    private static RateLimitDescriptor CreateFixedDescriptor(string name) =>
        new()
        {
            Name = name,
            Stage = Stage,
            Window = TimeSpan.FromDays(1),
            IsSliding = false,
            LimitKey = GetLimitKey(name),
            OverageKey = GetOverageKey(name),
            UsageMeter = "meter",
            OverageUsageMeter = "meter_overage",
            PartitionValueSelector = static _ => PartitionValue,
        };

    private static ExpiringCounterIncrement CreateFixedIncrement(string name, long limit) =>
        new()
        {
            Key = CreateKey(name, PartitionValue, DayStart),
            ExpiresWhen = DayStart.AddDays(1),
            Limit = limit,
        };

    private static string CreateKey(string partition, string value, DateTimeOffset windowStart) =>
        ExpiringKeys.Create(
            "rate-limit",
            TenantId,
            partition,
            value,
            windowStart.UtcTicks.ToString(CultureInfo.InvariantCulture)
        );

    private RateLimitDescriptor SetupLimitedStage()
    {
        var descriptor = CreateDescriptor("limited", usageMeter: "limited_meter");
        SetupStage(descriptor);
        SetupTenant();
        SetupLimit(MockTenantSettings, descriptor.LimitKey, 1);
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        return descriptor;
    }

    private long Timestamp { get; set; } = 1_000;

    // The breaker is timed on the monotonic timestamp, read only once the store has failed.
    private void SetupTimestamp() =>
        MockTimeProvider.Setup(x => x.GetTimestamp()).Returns(() => Timestamp).Verifiable();

    // The elapsed time since a failure is read only by a call that finds the breaker open.
    private void SetupTimestampFrequency() =>
        MockTimeProvider
            .Setup(x => x.TimestampFrequency)
            .Returns(TimeSpan.TicksPerSecond)
            .Verifiable();

    #endregion

    #region AcquireAsync Tests

    [Fact]
    public async Task AcquireAsync_WhenNoDescriptorHasALimit_AdmitsAndAddsItsUsageMeterWithoutTheStore()
    {
        var descriptor = CreateDescriptor("unlimited", usageMeter: "unlimited_meter");
        SetupStage(descriptor);
        SetupTenant(readsTenantId: false);
        SetupLimit(MockTenantSettings, descriptor.LimitKey, null);

        var decision = await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.False(decision.IsRejected);
        Assert.False(state.IsRefused);
        Assert.Equal(["unlimited_meter"], state.UsageMeters);
        Assert.Empty(Judgements);
    }

    [Fact]
    public async Task AcquireAsync_WhenALimitIsJudged_TagsTheMeasurementWithTheTenantEnvironmentType()
    {
        var descriptor = CreateDescriptor("tagged", usageMeter: "tagged_meter");
        SetupStage(descriptor);
        SetupTenant();
        SetupLimit(MockTenantSettings, descriptor.LimitKey, 10);
        SetupIncrements(
            [CreateSlidingIncrement("tagged", limit: 10)],
            new ExpiringCounterCount(1, 0)
        );

        await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        var measurement = Assert.Single(Collector.GetMeasurementSnapshot());
        Assert.Equal(
            OpenIdConstants.TenantEnvironmentTypes.Production,
            measurement.Tags.GetValueOrDefault(AuthenticationMetrics.TenantEnvironmentTypeAttribute)
        );
    }

    [Fact]
    public async Task AcquireAsync_WhenAUsageMeterIsAddedAgain_KeepsItOnce()
    {
        var first = CreateDescriptor("first", order: 100, usageMeter: "shared");
        var second = CreateDescriptor("second", order: 200, usageMeter: "shared");
        SetupStage(first, second);
        SetupTenant(readsTenantId: false);
        SetupLimit(MockTenantSettings, first.LimitKey, null);
        SetupLimit(MockTenantSettings, second.LimitKey, null);

        var firstDecision = await Limiter.AcquireAsync(
            Stage,
            CreateContext(),
            CancellationToken.None
        );
        var secondDecision = await Limiter.AcquireAsync(
            Stage,
            CreateContext(),
            CancellationToken.None
        );

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.False(firstDecision.IsRejected);
        Assert.False(secondDecision.IsRejected);
        Assert.Equal(["shared"], state.UsageMeters);
        Assert.Empty(Judgements);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheEstimateIsWithinTheLimit_AdmitsInOneBatch()
    {
        var descriptor = CreateDescriptor("within", usageMeter: "within_meter");
        SetupStage(descriptor);
        SetupTenant();
        SetupLimit(MockTenantSettings, descriptor.LimitKey, 10);
        SetupIncrements(
            [CreateSlidingIncrement("within", limit: 10)],
            new ExpiringCounterCount(7, 4)
        );

        var decision = await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.False(decision.IsRejected);
        Assert.False(state.IsRefused);
        Assert.Equal(["within_meter"], state.UsageMeters);
        Assert.Equal([("within", AuthenticationMetrics.AdmittedResult)], Judgements);
        Assert.Empty(LogEntries);
    }

    [Fact]
    public async Task AcquireAsync_WhenThePreviousWindowPushesTheEstimateOverTheLimit_RejectsUntilTheEstimateAdmitsAnother()
    {
        var descriptor = CreateDescriptor("crowded", usageMeter: "crowded_meter");
        SetupStage(descriptor);
        SetupTenant();
        SetupLimit(MockTenantSettings, descriptor.LimitKey, 10);
        SetupIncrements(
            [CreateSlidingIncrement("crowded", limit: 10)],
            new ExpiringCounterCount(3, 12)
        );

        var decision = await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.True(decision.IsRejected);
        Assert.Equal(TimeSpan.FromSeconds(15), decision.RetryAfter);
        Assert.True(state.IsRefused);
        Assert.Empty(state.UsageMeters);
        Assert.Equal([("crowded", AuthenticationMetrics.RejectedResult)], Judgements);
        Assert.Equal([(EventIds.RateLimitExceeded, LogLevel.Information)], LogEntries);
    }

    [Fact]
    public async Task AcquireAsync_WhenAnEarlierLimitRejects_StopsTheBatchThere()
    {
        var first = CreateDescriptor("first", order: 100, usageMeter: "first_meter");
        var second = CreateDescriptor("second", order: 200, usageMeter: "second_meter");
        SetupStage(first, second);
        SetupTenant();
        SetupLimit(MockTenantSettings, first.LimitKey, 10);
        SetupLimit(MockTenantSettings, second.LimitKey, 20);
        SetupIncrements(
            [
                CreateSlidingIncrement("first", limit: 10),
                CreateSlidingIncrement("second", limit: 20),
            ],
            new ExpiringCounterCount(11, 0)
        );

        var decision = await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.True(decision.IsRejected);
        Assert.True(state.IsRefused);
        Assert.Empty(state.UsageMeters);
        Assert.Equal([("first", AuthenticationMetrics.RejectedResult)], Judgements);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheEndpointIsNotMetered_SkipsAMeteredOnlyDescriptor()
    {
        var metered = CreateDescriptor(
            "metered",
            order: 100,
            isMeteredOnly: true,
            usageMeter: "metered_meter"
        );
        var plain = CreateDescriptor("plain", order: 200, usageMeter: "plain_meter");
        SetupStage(metered, plain);
        SetupTenant();
        SetupLimit(MockTenantSettings, plain.LimitKey, 10);
        SetupIncrements(
            [CreateSlidingIncrement("plain", limit: 10)],
            new ExpiringCounterCount(1, 0)
        );
        var metadata = new EndpointMetadataCollection(
            new DefaultOpenIdEndpointMeteredMetadata { IsMetered = false }
        );
        HttpContext.SetEndpoint(new Endpoint(null, metadata, "test"));

        var decision = await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.False(decision.IsRejected);
        Assert.Equal(["plain_meter"], state.UsageMeters);
        Assert.Equal([("plain", AuthenticationMetrics.AdmittedResult)], Judgements);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheSelectorReturnsNull_SkipsTheDescriptor()
    {
        var skipped = CreateDescriptor(
            "skipped",
            order: 100,
            usageMeter: "skipped_meter",
            selector: static _ => null
        );
        var applied = CreateDescriptor("applied", order: 200, usageMeter: "applied_meter");
        SetupStage(skipped, applied);
        SetupTenant();
        SetupLimit(MockTenantSettings, applied.LimitKey, 10);
        SetupIncrements(
            [CreateSlidingIncrement("applied", limit: 10)],
            new ExpiringCounterCount(1, 0)
        );

        var decision = await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.False(decision.IsRejected);
        Assert.Equal(["applied_meter"], state.UsageMeters);
        Assert.Equal([("applied", AuthenticationMetrics.AdmittedResult)], Judgements);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheContextNamesAClient_ReadsTheClientSettings()
    {
        var descriptor = CreateDescriptor("client_limited", usageMeter: "client_meter");
        var mockClientSettings = CreateStrictMock<IReadOnlySettingCollection>();
        SetupLimit(mockClientSettings, descriptor.LimitKey, 5);
        mockClientSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.EnvironmentType))
            .Returns(OpenIdConstants.TenantEnvironmentTypes.Production);
        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.Setup(x => x.Settings).Returns(mockClientSettings.Object).Verifiable();
        SetupStage(descriptor);

        // The tenant's settings provider has no setup: a context that names a client never reads it.
        SetupTenant(readsSettings: false);
        SetupIncrements(
            [CreateSlidingIncrement("client_limited", limit: 5)],
            new ExpiringCounterCount(1, 0)
        );

        var decision = await Limiter.AcquireAsync(
            Stage,
            CreateContext(mockClient.Object),
            CancellationToken.None
        );

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.False(decision.IsRejected);
        Assert.Equal(["client_meter"], state.UsageMeters);
        Assert.Equal([("client_limited", AuthenticationMetrics.AdmittedResult)], Judgements);
    }

    [Fact]
    public async Task AcquireAsync_WhenAFixedWindowIsWithinItsLimit_AdmitsAndAddsItsUsageMeter()
    {
        var descriptor = CreateFixedDescriptor("daily");
        SetupStage(descriptor);
        SetupTenant();
        SetupLimit(MockTenantSettings, descriptor.LimitKey, 5);
        SetupLimit(MockTenantSettings, GetOverageKey("daily"), null);
        SetupIncrements([CreateFixedIncrement("daily", limit: 5)], new ExpiringCounterCount(5, 0));

        var decision = await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.False(decision.IsRejected);
        Assert.False(state.IsRefused);
        Assert.Equal(["meter"], state.UsageMeters);
        Assert.Equal([("daily", AuthenticationMetrics.AdmittedResult)], Judgements);
        Assert.Empty(LogEntries);
    }

    [Fact]
    public async Task AcquireAsync_WhenAFixedWindowIsPastItsLimitWithinTheOverage_AdmitsAndAddsTheOverageMeter()
    {
        var descriptor = CreateFixedDescriptor("daily");
        SetupStage(descriptor);
        SetupTenant();
        SetupLimit(MockTenantSettings, descriptor.LimitKey, 5);
        SetupLimit(MockTenantSettings, GetOverageKey("daily"), 2);
        SetupIncrements([CreateFixedIncrement("daily", limit: 7)], new ExpiringCounterCount(6, 0));

        var decision = await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.False(decision.IsRejected);
        Assert.False(state.IsRefused);
        Assert.Equal(["meter", "meter_overage"], state.UsageMeters);
        Assert.Equal([("daily", AuthenticationMetrics.OverageResult)], Judgements);
        Assert.Empty(LogEntries);
    }

    [Fact]
    public async Task AcquireAsync_WhenAFixedWindowIsPastItsLimitAndOverage_RejectsUntilTheWindowEnds()
    {
        var descriptor = CreateFixedDescriptor("daily");
        SetupStage(descriptor);
        SetupTenant();
        SetupLimit(MockTenantSettings, descriptor.LimitKey, 5);
        SetupLimit(MockTenantSettings, GetOverageKey("daily"), 2);
        SetupIncrements([CreateFixedIncrement("daily", limit: 7)], new ExpiringCounterCount(8, 0));

        var decision = await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.True(decision.IsRejected);
        Assert.Equal(new TimeSpan(11, 59, 45), decision.RetryAfter);
        Assert.True(state.IsRefused);
        Assert.Empty(state.UsageMeters);
        Assert.Equal([("daily", AuthenticationMetrics.RejectedResult)], Judgements);
        Assert.Equal([(EventIds.RateLimitExceeded, LogLevel.Information)], LogEntries);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheStoreThrows_AdmitsEveryLimitUncountedAndLogsAWarning()
    {
        SetupLimitedStage();
        SetupTimestamp();
        MockCounterSet
            .Setup(x =>
                x.IncrementUntilExceededAsync(
                    It.IsAny<IReadOnlyList<ExpiringCounterIncrement>>(),
                    CancellationToken.None
                )
            )
            .ThrowsAsync(new InvalidOperationException("The store is unavailable."))
            .Verifiable();

        var decision = await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.False(decision.IsRejected);
        Assert.False(state.IsRefused);
        Assert.Equal(["limited_meter"], state.UsageMeters);
        Assert.Equal([("limited", AuthenticationMetrics.StoreUnavailableResult)], Judgements);
        Assert.Equal([(EventIds.RateLimitStoreUnavailable, LogLevel.Warning)], LogEntries);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheStoreThrewWithinTheRetryInterval_AdmitsWithoutCallingIt()
    {
        SetupLimitedStage();
        SetupTimestamp();
        SetupTimestampFrequency();
        MockCounterSet
            .Setup(x =>
                x.IncrementUntilExceededAsync(
                    It.IsAny<IReadOnlyList<ExpiringCounterIncrement>>(),
                    CancellationToken.None
                )
            )
            .ThrowsAsync(new InvalidOperationException("The store is unavailable."))
            .Verifiable(Times.Once);

        await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);
        var decision = await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.False(decision.IsRejected);
        Assert.False(state.IsRefused);
        Assert.Equal(["limited_meter"], state.UsageMeters);
        Assert.Equal(
            [
                ("limited", AuthenticationMetrics.StoreUnavailableResult),
                ("limited", AuthenticationMetrics.StoreUnavailableResult),
            ],
            Judgements
        );
        Assert.Equal([(EventIds.RateLimitStoreUnavailable, LogLevel.Warning)], LogEntries);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheStoreAnswersAfterTheRetryInterval_EnforcesAndLogsTheRecovery()
    {
        SetupLimitedStage();
        SetupTimestamp();
        SetupTimestampFrequency();
        IReadOnlyList<ExpiringCounterIncrement> expected =
        [
            CreateSlidingIncrement("limited", limit: 1),
        ];
        IReadOnlyList<ExpiringCounterCount> answer = [new ExpiringCounterCount(2, 0)];
        MockCounterSet
            .SetupSequence(x =>
                x.IncrementUntilExceededAsync(
                    It.Is<IReadOnlyList<ExpiringCounterIncrement>>(actual =>
                        actual.SequenceEqual(expected)
                    ),
                    CancellationToken.None
                )
            )
            .ThrowsAsync(new InvalidOperationException("The store is unavailable."))
            .ReturnsAsync(answer);

        await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);
        Timestamp += DefaultRequestRateLimiter.StoreRetryInterval.Ticks;
        var decision = await Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);

        MockCounterSet.Verify(
            x =>
                x.IncrementUntilExceededAsync(
                    It.Is<IReadOnlyList<ExpiringCounterIncrement>>(actual =>
                        actual.SequenceEqual(expected)
                    ),
                    CancellationToken.None
                ),
            Times.Exactly(2)
        );
        var state = RateLimitRequestState.Get(HttpContext);
        Assert.True(decision.IsRejected);
        Assert.True(state.IsRefused);
        Assert.Equal(["limited_meter"], state.UsageMeters);
        Assert.Equal(
            [
                ("limited", AuthenticationMetrics.StoreUnavailableResult),
                ("limited", AuthenticationMetrics.RejectedResult),
            ],
            Judgements
        );
        Assert.Equal(
            [
                (EventIds.RateLimitStoreUnavailable, LogLevel.Warning),
                (EventIds.RateLimitStoreRecovered, LogLevel.Information),
                (EventIds.RateLimitExceeded, LogLevel.Information),
            ],
            LogEntries
        );
    }

    [Fact]
    public async Task AcquireAsync_WhenTheStoreDoesNotAnswerInTime_Admits()
    {
        SetupLimitedStage();
        SetupTimestamp();
        TimerCallback? timerCallback = null;
        object? timerState = null;
        MockTimeProvider
            .Setup(x =>
                x.CreateTimer(
                    It.IsAny<TimerCallback>(),
                    It.IsAny<object?>(),
                    DefaultRequestRateLimiter.StoreTimeout,
                    It.IsAny<TimeSpan>()
                )
            )
            .Callback<TimerCallback, object?, TimeSpan, TimeSpan>(
                (callback, state, _, _) =>
                {
                    timerCallback = callback;
                    timerState = state;
                }
            )
            .Returns(CreateLooseMock<ITimer>().Object)
            .Verifiable();
        var unanswered = new TaskCompletionSource<IReadOnlyList<ExpiringCounterCount>>(
            TaskCreationOptions.RunContinuationsAsynchronously
        );
        MockCounterSet
            .Setup(x =>
                x.IncrementUntilExceededAsync(
                    It.IsAny<IReadOnlyList<ExpiringCounterIncrement>>(),
                    CancellationToken.None
                )
            )
            .Returns(new ValueTask<IReadOnlyList<ExpiringCounterCount>>(unanswered.Task))
            .Verifiable();

        var pending = Limiter.AcquireAsync(Stage, CreateContext(), CancellationToken.None);
        Assert.False(pending.IsCompleted);
        Assert.NotNull(timerCallback);
        timerCallback(timerState);
        var decision = await pending;

        // The abandoned call's eventual failure is observed rather than left unobserved.
        unanswered.SetException(new TimeoutException("The store timed out."));

        var state = RateLimitRequestState.Get(HttpContext);
        Assert.False(decision.IsRejected);
        Assert.Equal(["limited_meter"], state.UsageMeters);
        Assert.Equal([("limited", AuthenticationMetrics.StoreUnavailableResult)], Judgements);
        Assert.Equal([(EventIds.RateLimitStoreUnavailable, LogLevel.Warning)], LogEntries);
    }

    [Fact]
    public async Task AcquireAsync_WhenTheStoreCallIsCancelled_Throws()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        SetupLimitedStage();
        MockCounterSet
            .Setup(x =>
                x.IncrementUntilExceededAsync(
                    It.IsAny<IReadOnlyList<ExpiringCounterIncrement>>(),
                    cancellationSource.Token
                )
            )
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token))
            .Verifiable();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Limiter.AcquireAsync(Stage, CreateContext(), cancellationSource.Token)
        );

        // A cancelled call is not a store failure: it opens no breaker and judges nothing.
        Assert.Empty(Judgements);
        Assert.Empty(LogEntries);
    }

    #endregion
}
