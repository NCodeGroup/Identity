// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Core.Tests.SharedState;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Clients;

public sealed class DefaultClientAuthenticationAttemptLimiterTests : BaseTests
{
    private const string TenantId = "tenant-1";

    private const string ClientId = "client-1";

    private const int ClientLimit = 3;

    private const int AddressLimit = 5;

    private static TimeSpan Window { get; } = TimeSpan.FromMinutes(15);

    // Five minutes into a fifteen-minute window, so the window ends ten minutes later.
    private static DateTimeOffset Now { get; } = new(2026, 11, 3, 12, 5, 0, TimeSpan.Zero);

    private FakeTimeProvider TimeProvider { get; } = new(Now);

    private FakeLogger<DefaultClientAuthenticationAttemptLimiter> Logger { get; } = new();

    private TestExpiringCounterSet CounterSet { get; }

    private DefaultClientAuthenticationAttemptLimiter Limiter { get; }

    public DefaultClientAuthenticationAttemptLimiterTests()
    {
        CounterSet = new TestExpiringCounterSet(TimeProvider);
        Limiter = new DefaultClientAuthenticationAttemptLimiter(CounterSet, TimeProvider, Logger);
    }

    #region Scaffolding

    private OpenIdContext CreateContext(string? remoteAddress)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = remoteAddress is null
            ? null
            : IPAddress.Parse(remoteAddress);

        var mockSettings = CreateLooseMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.ClientAuthenticationFailedAttemptsLimit))
            .Returns(ClientLimit);
        mockSettings
            .Setup(x =>
                x.GetValue(OpenIdSettingKeys.ClientAuthenticationFailedAttemptsAddressLimit)
            )
            .Returns(AddressLimit);
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.ClientAuthenticationFailedAttemptsWindow))
            .Returns(Window);

        var mockProvider = CreateLooseMock<IReadOnlySettingCollectionProvider>();
        mockProvider.Setup(x => x.Collection).Returns(mockSettings.Object);

        var mockTenant = CreateLooseMock<OpenIdTenant>();
        mockTenant.Setup(x => x.TenantId).Returns(TenantId);
        mockTenant.Setup(x => x.SettingsProvider).Returns(mockProvider.Object);

        var mockContext = CreateLooseMock<OpenIdContext>();
        mockContext.Setup(x => x.Http).Returns(httpContext);
        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object);
        return mockContext.Object;
    }

    private async Task RecordFailuresAsync(string remoteAddress, string? clientId, int count)
    {
        for (var i = 0; i < count; i++)
            await Limiter.RecordFailureAsync(
                CreateContext(remoteAddress),
                clientId,
                CancellationToken.None
            );
    }

    #endregion

    #region CheckAsync Tests

    [Fact]
    public async Task CheckAsync_WithoutFailures_AdmitsWithOneRead()
    {
        var decision = await Limiter.CheckAsync(
            CreateContext("203.0.113.5"),
            ClientId,
            CancellationToken.None
        );

        Assert.False(decision.IsRejected);
        Assert.Equal(1, CounterSet.ReadCount);
    }

    [Fact]
    public async Task CheckAsync_WithoutARemoteAddress_AdmitsWithoutReading()
    {
        var mockCounterSet = CreateStrictMock<IExpiringCounterSet>();
        var limiter = new DefaultClientAuthenticationAttemptLimiter(
            mockCounterSet.Object,
            TimeProvider,
            Logger
        );

        var decision = await limiter.CheckAsync(
            CreateContext(null),
            ClientId,
            CancellationToken.None
        );

        Assert.False(decision.IsRejected);
    }

    [Fact]
    public async Task CheckAsync_WithoutARemoteAddress_WarnsOnce()
    {
        // The limit is off for every request such a host sees, so the operator is told once, never per request.
        var mockCounterSet = CreateStrictMock<IExpiringCounterSet>();
        var limiter = new DefaultClientAuthenticationAttemptLimiter(
            mockCounterSet.Object,
            TimeProvider,
            Logger
        );

        await limiter.CheckAsync(CreateContext(null), ClientId, CancellationToken.None);
        await limiter.RecordFailureAsync(CreateContext(null), ClientId, CancellationToken.None);
        await limiter.CheckAsync(CreateContext(null), ClientId, CancellationToken.None);

        var record = Assert.Single(Logger.Collector.GetSnapshot());
        Assert.Equal(
            EventIds.ClientAuthenticationAttemptLimitSkippedWithoutRemoteAddress,
            record.Id.Id
        );
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Contains(TenantId, record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsync_WhenTheAddressAndClientReachedTheLimit_RefusesUntilTheWindowEnds()
    {
        await RecordFailuresAsync("203.0.113.5", ClientId, ClientLimit);

        var decision = await Limiter.CheckAsync(
            CreateContext("203.0.113.5"),
            ClientId,
            CancellationToken.None
        );

        Assert.True(decision.IsRejected);
        Assert.Equal(TimeSpan.FromMinutes(10), decision.RetryAfter);
    }

    [Fact]
    public async Task CheckAsync_WhenBelowTheClientLimit_Admits()
    {
        await RecordFailuresAsync("203.0.113.5", ClientId, ClientLimit - 1);

        var decision = await Limiter.CheckAsync(
            CreateContext("203.0.113.5"),
            ClientId,
            CancellationToken.None
        );

        Assert.False(decision.IsRejected);
    }

    [Fact]
    public async Task CheckAsync_WhenTheClientReachedTheLimitFromAnotherAddress_Admits()
    {
        await RecordFailuresAsync("203.0.113.5", ClientId, ClientLimit);

        var decision = await Limiter.CheckAsync(
            CreateContext("198.51.100.7"),
            ClientId,
            CancellationToken.None
        );

        Assert.False(decision.IsRejected);
    }

    [Fact]
    public async Task CheckAsync_WhenTheAddressReachedTheLimitForAnotherClient_AdmitsThisClient()
    {
        await RecordFailuresAsync("203.0.113.5", "client-2", ClientLimit);

        var decision = await Limiter.CheckAsync(
            CreateContext("203.0.113.5"),
            ClientId,
            CancellationToken.None
        );

        Assert.False(decision.IsRejected);
    }

    [Fact]
    public async Task CheckAsync_WhenTheAddressReachedItsLimitAcrossClients_RefusesEveryClient()
    {
        for (var i = 0; i < AddressLimit; i++)
            await RecordFailuresAsync("203.0.113.5", $"guessed-{i}", 1);

        var decision = await Limiter.CheckAsync(
            CreateContext("203.0.113.5"),
            ClientId,
            CancellationToken.None
        );

        Assert.True(decision.IsRejected);
    }

    [Fact]
    public async Task CheckAsync_WhenTheAddressReachedItsLimit_RefusesARequestThatNamesNoClient()
    {
        await RecordFailuresAsync("203.0.113.5", null, AddressLimit);

        var decision = await Limiter.CheckAsync(
            CreateContext("203.0.113.5"),
            null,
            CancellationToken.None
        );

        Assert.True(decision.IsRejected);
    }

    [Fact]
    public async Task CheckAsync_WhenAnIPv6AddressOfTheSameSlash64ReachedTheLimit_Refuses()
    {
        await RecordFailuresAsync("2001:db8:1:2::1", ClientId, ClientLimit);

        var decision = await Limiter.CheckAsync(
            CreateContext("2001:db8:1:2::ffff"),
            ClientId,
            CancellationToken.None
        );

        Assert.True(decision.IsRejected);
    }

    [Fact]
    public async Task CheckAsync_AfterTheWindowEnds_Admits()
    {
        await RecordFailuresAsync("203.0.113.5", ClientId, ClientLimit);
        TimeProvider.Advance(TimeSpan.FromMinutes(10));

        var decision = await Limiter.CheckAsync(
            CreateContext("203.0.113.5"),
            ClientId,
            CancellationToken.None
        );

        Assert.False(decision.IsRejected);
    }

    [Fact]
    public async Task CheckAsync_WhenRefused_MarksTheRequestRefusedAndLogsThePartition()
    {
        await RecordFailuresAsync("203.0.113.5", ClientId, ClientLimit);
        var context = CreateContext("203.0.113.5");

        await Limiter.CheckAsync(context, ClientId, CancellationToken.None);

        Assert.True(RateLimitRequestState.Get(context.Http).IsRefused);
        var record = Assert.Single(Logger.Collector.GetSnapshot());
        Assert.Equal(EventIds.ClientAuthenticationAttemptLimitReached, record.Id.Id);
        Assert.Contains(
            DefaultClientAuthenticationAttemptLimiter.AddressClientPartition,
            record.Message
        );
        Assert.DoesNotContain(ClientId, record.Message);
        Assert.DoesNotContain("203.0.113.5", record.Message);
    }

    [Fact]
    public async Task CheckAsync_AskedTwiceInOneRequest_ReadsEachCounterOnce()
    {
        await RecordFailuresAsync("203.0.113.5", ClientId, ClientLimit - 1);
        var context = CreateContext("203.0.113.5");

        await Limiter.CheckAsync(context, ClientId, CancellationToken.None);
        await Limiter.CheckAsync(context, ClientId, CancellationToken.None);

        Assert.Equal(1, CounterSet.ReadCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheStoreThrows_Propagates()
    {
        var mockCounterSet = CreateStrictMock<IExpiringCounterSet>();
        mockCounterSet
            .Setup(x => x.GetAsync(It.IsAny<string>(), CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("store down"))
            .Verifiable();
        var limiter = new DefaultClientAuthenticationAttemptLimiter(
            mockCounterSet.Object,
            TimeProvider,
            Logger
        );

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await limiter.CheckAsync(CreateContext("203.0.113.5"), ClientId, CancellationToken.None)
        );
    }

    #endregion

    #region RecordFailureAsync Tests

    [Fact]
    public async Task RecordFailureAsync_CountsBothPartitionsInOneCall()
    {
        await Limiter.RecordFailureAsync(
            CreateContext("203.0.113.5"),
            ClientId,
            CancellationToken.None
        );

        Assert.Equal(1, CounterSet.IncrementCallCount);
        var windowStart = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(
            1,
            await CounterSet.GetAsync(
                DefaultClientAuthenticationAttemptLimiter.CreateAddressKey(
                    TenantId,
                    "203.0.113.5",
                    windowStart
                ),
                CancellationToken.None
            )
        );
        Assert.Equal(
            1,
            await CounterSet.GetAsync(
                DefaultClientAuthenticationAttemptLimiter.CreateAddressClientKey(
                    TenantId,
                    "203.0.113.5",
                    ClientId,
                    windowStart
                ),
                CancellationToken.None
            )
        );
    }

    [Fact]
    public async Task RecordFailureAsync_WithoutARemoteAddress_CountsNothing()
    {
        var mockCounterSet = CreateStrictMock<IExpiringCounterSet>();
        var limiter = new DefaultClientAuthenticationAttemptLimiter(
            mockCounterSet.Object,
            TimeProvider,
            Logger
        );

        await limiter.RecordFailureAsync(CreateContext(null), ClientId, CancellationToken.None);
    }

    #endregion
}
