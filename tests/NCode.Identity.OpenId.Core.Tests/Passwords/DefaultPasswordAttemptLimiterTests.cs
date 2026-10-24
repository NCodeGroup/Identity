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

using Microsoft.Extensions.Logging.Testing;
using Moq;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Passwords;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.OpenId.Tenants;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Passwords;

public sealed class DefaultPasswordAttemptLimiterTests : BaseTests
{
    private const string TenantId = "tenant-1";

    private const string UserName = "alice";

    private static DateTimeOffset Now { get; } = new(2026, 10, 23, 12, 0, 0, TimeSpan.Zero);

    private Mock<IExpiringCounterSet> MockCounterSet { get; }

    private Mock<TimeProvider> MockTimeProvider { get; }

    private FakeLogger<DefaultPasswordAttemptLimiter> Logger { get; } = new();

    private DefaultPasswordAttemptLimiter Limiter { get; }

    public DefaultPasswordAttemptLimiterTests()
    {
        MockCounterSet = CreateStrictMock<IExpiringCounterSet>();
        MockTimeProvider = CreateStrictMock<TimeProvider>();
        Limiter = new DefaultPasswordAttemptLimiter(
            MockCounterSet.Object,
            MockTimeProvider.Object,
            Logger
        );
    }

    #region Scaffolding

    private OpenIdContext CreateContext(Mock<IReadOnlySettingCollection> mockSettings)
    {
        var mockProvider = CreateStrictMock<IReadOnlySettingCollectionProvider>();
        mockProvider.Setup(x => x.Collection).Returns(mockSettings.Object).Verifiable();

        var mockTenant = CreateStrictMock<OpenIdTenant>();
        mockTenant.Setup(x => x.TenantId).Returns(TenantId).Verifiable();
        mockTenant.Setup(x => x.SettingsProvider).Returns(mockProvider.Object).Verifiable();

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.Setup(x => x.Tenant).Returns(mockTenant.Object).Verifiable();

        return mockContext.Object;
    }

    private OpenIdContext CreateContextWithLimit(int limit)
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.PasswordFailedAttemptsLimit))
            .Returns(limit)
            .Verifiable();

        return CreateContext(mockSettings);
    }

    private OpenIdContext CreateContextWithWindow(TimeSpan window)
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.PasswordFailedAttemptsWindow))
            .Returns(window)
            .Verifiable();

        return CreateContext(mockSettings);
    }

    #endregion

    #region IsLimitReachedAsync Tests

    [Fact]
    public async Task IsLimitReachedAsync_WhenBelowTheLimit_ReturnsFalse()
    {
        var context = CreateContextWithLimit(3);
        var key = DefaultPasswordAttemptLimiter.CreateKey(TenantId, UserName);
        MockCounterSet
            .Setup(x => x.GetAsync(key, CancellationToken.None))
            .ReturnsAsync(2)
            .Verifiable();

        var result = await Limiter.IsLimitReachedAsync(context, UserName, CancellationToken.None);

        Assert.False(result);
        Assert.Empty(Logger.Collector.GetSnapshot());
    }

    [Fact]
    public async Task IsLimitReachedAsync_WhenTheLimitIsReached_ReturnsTrueAndLogs()
    {
        var context = CreateContextWithLimit(3);
        var key = DefaultPasswordAttemptLimiter.CreateKey(TenantId, UserName);
        MockCounterSet
            .Setup(x => x.GetAsync(key, CancellationToken.None))
            .ReturnsAsync(3)
            .Verifiable();

        var result = await Limiter.IsLimitReachedAsync(context, UserName, CancellationToken.None);

        Assert.True(result);
        var record = Assert.Single(Logger.Collector.GetSnapshot());
        Assert.Equal(EventIds.PasswordAttemptLimitReached, record.Id.Id);
    }

    [Fact]
    public async Task IsLimitReachedAsync_WhenTheCounterStoreFails_Throws()
    {
        var context = CreateContextWithLimit(3);
        var key = DefaultPasswordAttemptLimiter.CreateKey(TenantId, UserName);
        MockCounterSet
            .Setup(x => x.GetAsync(key, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("The counter store is down."))
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Limiter.IsLimitReachedAsync(context, UserName, CancellationToken.None)
        );

        Assert.Empty(Logger.Collector.GetSnapshot());
    }

    #endregion

    #region RecordFailureAsync Tests

    [Fact]
    public async Task RecordFailureAsync_IncrementsTheCounterUntilTheWindowEnds()
    {
        var window = TimeSpan.FromMinutes(15);
        var context = CreateContextWithWindow(window);
        var key = DefaultPasswordAttemptLimiter.CreateKey(TenantId, UserName);
        var expiresWhen = Now + window;
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        MockCounterSet
            .Setup(x => x.IncrementAsync(key, expiresWhen, CancellationToken.None))
            .ReturnsAsync(1)
            .Verifiable();

        await Limiter.RecordFailureAsync(context, UserName, CancellationToken.None);
    }

    [Fact]
    public async Task RecordFailureAsync_WhenTheCounterStoreFails_Throws()
    {
        var window = TimeSpan.FromMinutes(15);
        var context = CreateContextWithWindow(window);
        var key = DefaultPasswordAttemptLimiter.CreateKey(TenantId, UserName);
        var expiresWhen = Now + window;
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        MockCounterSet
            .Setup(x => x.IncrementAsync(key, expiresWhen, CancellationToken.None))
            .ThrowsAsync(new InvalidOperationException("The counter store is down."))
            .Verifiable();

        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await Limiter.RecordFailureAsync(context, UserName, CancellationToken.None)
        );
    }

    #endregion

    #region ResetAsync Tests

    [Fact]
    public async Task ResetAsync_RemovesTheCounter()
    {
        var key = DefaultPasswordAttemptLimiter.CreateKey(TenantId, UserName);
        MockCounterSet
            .Setup(x => x.RemoveAsync(key, CancellationToken.None))
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        await Limiter.ResetAsync(TenantId, UserName, CancellationToken.None);
    }

    #endregion

    #region CreateKey Tests

    [Theory]
    [InlineData("alice", "ALICE")]
    [InlineData("\u212Aate", "kate")]
    [InlineData("\u017Fam", "sam")]
    [InlineData("jose\u0301", "jos\u00E9")]
    public void CreateKey_FoldsEverySpellingOfOneName(string first, string second)
    {
        var firstKey = DefaultPasswordAttemptLimiter.CreateKey(TenantId, first);
        var secondKey = DefaultPasswordAttemptLimiter.CreateKey(TenantId, second);

        Assert.Equal(firstKey, secondKey);
    }

    [Fact]
    public void CreateKey_SeparatesTenants()
    {
        var firstKey = DefaultPasswordAttemptLimiter.CreateKey("a", UserName);
        var secondKey = DefaultPasswordAttemptLimiter.CreateKey("b", UserName);

        Assert.NotEqual(firstKey, secondKey);
    }

    #endregion
}
