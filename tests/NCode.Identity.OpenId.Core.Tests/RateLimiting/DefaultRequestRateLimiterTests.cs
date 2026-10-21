#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Moq;
using NCode.Identity.OpenId.Authentication.Clients;
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

    private const string ClientId = "client-1";

    private const string RemoteAddress = "192.0.2.1";

    // Fifteen seconds into the minute, so the previous window weighs three quarters.
    private static DateTimeOffset WindowStart { get; } = new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero);

    private static DateTimeOffset Now { get; } = WindowStart.AddSeconds(15);

    private Mock<IExpiringCounterSet> MockCounterSet { get; }

    private Mock<TimeProvider> MockTimeProvider { get; }

    private Mock<OpenIdContext> MockContext { get; }

    private Mock<IReadOnlySettingCollection> MockTenantSettings { get; }

    private DefaultRequestRateLimiter Limiter { get; }

    public DefaultRequestRateLimiterTests()
    {
        MockCounterSet = CreateStrictMock<IExpiringCounterSet>();
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        MockContext = CreateStrictMock<OpenIdContext>();
        MockTenantSettings = CreateStrictMock<IReadOnlySettingCollection>();
        Limiter = new DefaultRequestRateLimiter(
            MockCounterSet.Object,
            MockTimeProvider.Object,
            CreateLooseMock<ILogger<DefaultRequestRateLimiter>>().Object
        );
    }

    #region Scaffolding

    private void SetupTenant(bool readsSettings = true)
    {
        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        if (readsSettings)
        {
            var mockProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
            mockProvider.Setup(x => x.Collection).Returns(MockTenantSettings.Object).Verifiable();
            mockTenant.Setup(x => x.SettingsProvider).Returns(mockProvider.Object).Verifiable();
        }

        MockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();
    }

    private void SetupRemoteAddress(string remoteAddress = RemoteAddress)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Connection.RemoteIpAddress = IPAddress.Parse(remoteAddress);
        MockContext.Setup(x => x.Http).Returns(httpContext).Verifiable();
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

    private void SetupCounters(string partition, string value, long current, long previous)
    {
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        MockCounterSet
            .Setup(x =>
                x.IncrementAsync(
                    CreateKey(partition, value, WindowStart),
                    WindowStart.AddMinutes(2),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(current)
            .Verifiable();
        MockCounterSet
            .Setup(x =>
                x.GetAsync(
                    CreateKey(partition, value, WindowStart.AddMinutes(-1)),
                    CancellationToken.None
                )
            )
            .ReturnsAsync(previous)
            .Verifiable();
    }

    private static string CreateKey(string partition, string value, DateTimeOffset windowStart) =>
        ExpiringKeys.Create(
            "rate-limit",
            TenantId,
            partition,
            value,
            windowStart.UtcTicks.ToString(CultureInfo.InvariantCulture)
        );

    #endregion

    #region AcquireForTenantAsync Tests

    [Fact]
    public async Task AcquireForTenantAsync_WhenNoLimitIsSet_AdmitsWithoutCounting()
    {
        SetupTenant();
        SetupRemoteAddress();
        SetupLimit(MockTenantSettings, OpenIdSettingKeys.RateLimitTenantPerMinute, null);
        SetupLimit(MockTenantSettings, OpenIdSettingKeys.RateLimitAddressPerMinute, null);

        var decision = await Limiter.AcquireForTenantAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.False(decision.IsRejected);
    }

    [Fact]
    public async Task AcquireForTenantAsync_WhenTheEstimateIsWithinTheLimit_Admits()
    {
        SetupTenant();
        SetupRemoteAddress();
        SetupLimit(MockTenantSettings, OpenIdSettingKeys.RateLimitTenantPerMinute, 10);
        SetupLimit(MockTenantSettings, OpenIdSettingKeys.RateLimitAddressPerMinute, null);
        // 4 × 0.75 + 7 = 10, which is the limit.
        SetupCounters(
            DefaultRequestRateLimiter.TenantPartition,
            string.Empty,
            current: 7,
            previous: 4
        );

        var decision = await Limiter.AcquireForTenantAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.False(decision.IsRejected);
    }

    [Fact]
    public async Task AcquireForTenantAsync_WhenThePreviousWindowPushesTheEstimateOverTheLimit_RejectsUntilTheEstimateAdmitsAnother()
    {
        SetupTenant();
        SetupRemoteAddress();
        SetupLimit(MockTenantSettings, OpenIdSettingKeys.RateLimitAddressPerMinute, null);
        SetupLimit(MockTenantSettings, OpenIdSettingKeys.RateLimitTenantPerMinute, 10);
        // 12 × 0.75 + 3 = 12, over the limit although the current window alone is not. One more request is admitted
        // once 12 × (1 − e / 60 s) + 4 ≤ 10, half a minute into the window: fifteen seconds from now.
        SetupCounters(
            DefaultRequestRateLimiter.TenantPartition,
            string.Empty,
            current: 3,
            previous: 12
        );

        var decision = await Limiter.AcquireForTenantAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(decision.IsRejected);
        Assert.Equal(TimeSpan.FromSeconds(15), decision.RetryAfter);
    }

    [Fact]
    public async Task AcquireForTenantAsync_WhenTheAddressIsOverItsLimit_RejectsWithoutCountingTheTenant()
    {
        SetupTenant();
        SetupRemoteAddress();
        SetupLimit(MockTenantSettings, OpenIdSettingKeys.RateLimitAddressPerMinute, 1);
        SetupCounters(
            DefaultRequestRateLimiter.AddressPartition,
            RemoteAddress,
            current: 2,
            previous: 0
        );

        var decision = await Limiter.AcquireForTenantAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(decision.IsRejected);
        MockCounterSet.Verify(
            x =>
                x.IncrementAsync(
                    CreateKey(DefaultRequestRateLimiter.TenantPartition, string.Empty, WindowStart),
                    It.IsAny<DateTimeOffset>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task AcquireForTenantAsync_WhenTheAddressIsIPv6_CountsItsSlash64()
    {
        SetupTenant();
        SetupRemoteAddress("2001:db8:1:2:3:4:5:6");
        SetupLimit(MockTenantSettings, OpenIdSettingKeys.RateLimitAddressPerMinute, 1);
        SetupCounters(
            DefaultRequestRateLimiter.AddressPartition,
            "2001:db8:1:2::/64",
            current: 2,
            previous: 0
        );

        var decision = await Limiter.AcquireForTenantAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.True(decision.IsRejected);
    }

    [Fact]
    public async Task AcquireForTenantAsync_WhenTheCounterStoreThrows_Admits()
    {
        SetupTenant();
        SetupRemoteAddress();
        SetupLimit(MockTenantSettings, OpenIdSettingKeys.RateLimitTenantPerMinute, 1);
        SetupLimit(MockTenantSettings, OpenIdSettingKeys.RateLimitAddressPerMinute, null);
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        MockCounterSet
            .Setup(x =>
                x.IncrementAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    CancellationToken.None
                )
            )
            .ThrowsAsync(new InvalidOperationException("The store is unavailable."))
            .Verifiable();

        var decision = await Limiter.AcquireForTenantAsync(
            MockContext.Object,
            CancellationToken.None
        );

        Assert.False(decision.IsRejected);
    }

    [Fact]
    public async Task AcquireForTenantAsync_WhenCancelled_Throws()
    {
        using var cancellationSource = new CancellationTokenSource();
        await cancellationSource.CancelAsync();
        SetupTenant();
        SetupRemoteAddress();
        SetupLimit(MockTenantSettings, OpenIdSettingKeys.RateLimitAddressPerMinute, null);
        SetupLimit(MockTenantSettings, OpenIdSettingKeys.RateLimitTenantPerMinute, 1);
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        MockCounterSet
            .Setup(x =>
                x.IncrementAsync(
                    It.IsAny<string>(),
                    It.IsAny<DateTimeOffset>(),
                    cancellationSource.Token
                )
            )
            .ThrowsAsync(new OperationCanceledException(cancellationSource.Token))
            .Verifiable();

        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await Limiter.AcquireForTenantAsync(MockContext.Object, cancellationSource.Token)
        );
    }

    #endregion

    #region AcquireForClientAsync Tests

    [Fact]
    public async Task AcquireForClientAsync_WhenNoLimitIsSet_AdmitsWithoutCounting()
    {
        var mockClientSettings = CreateStrictMock<IReadOnlySettingCollection>();
        SetupLimit(mockClientSettings, OpenIdSettingKeys.RateLimitClientPerMinute, null);
        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.Setup(x => x.Settings).Returns(mockClientSettings.Object).Verifiable();

        var decision = await Limiter.AcquireForClientAsync(
            MockContext.Object,
            mockClient.Object,
            CancellationToken.None
        );

        Assert.False(decision.IsRejected);
    }

    [Fact]
    public async Task AcquireForClientAsync_WhenTheClientIsOverItsLimit_Rejects()
    {
        SetupTenant(readsSettings: false);
        var mockClientSettings = CreateStrictMock<IReadOnlySettingCollection>();
        SetupLimit(mockClientSettings, OpenIdSettingKeys.RateLimitClientPerMinute, 5);
        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.Setup(x => x.Settings).Returns(mockClientSettings.Object).Verifiable();
        mockClient.Setup(x => x.ClientId).Returns(ClientId).Verifiable();
        // The current window alone is over the limit, so it becomes the next window's previous: one more request is
        // admitted once 6 × (1 − e / 60 s) + 1 ≤ 5, twenty seconds into the next window: 65 seconds from now.
        SetupCounters(DefaultRequestRateLimiter.ClientPartition, ClientId, current: 6, previous: 0);

        var decision = await Limiter.AcquireForClientAsync(
            MockContext.Object,
            mockClient.Object,
            CancellationToken.None
        );

        Assert.True(decision.IsRejected);
        Assert.Equal(TimeSpan.FromSeconds(65), decision.RetryAfter);
    }

    #endregion
}
