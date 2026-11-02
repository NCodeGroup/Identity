// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Persistence;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Usage;

public sealed class DefaultTenantUsageRecorderTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 23, 59, 59, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 21);

    private MockRepository MockRepository { get; }
    private Mock<TimeProvider> MockTimeProvider { get; }
    private DefaultTenantUsageRecorder Recorder { get; }

    public DefaultTenantUsageRecorderTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockTimeProvider = MockRepository.Create<TimeProvider>();
        Recorder = new DefaultTenantUsageRecorder(MockTimeProvider.Object);
    }

    public void Dispose() => MockRepository.Verify();

    #region Record Tests

    [Fact]
    public void Record_CountsEachTenantDayAndMeter()
    {
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();

        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        Recorder.Record("tenant-1", TenantUsageMeters.TokenRequests);
        Recorder.Record("tenant-2", TenantUsageMeters.Requests);

        var usage = Recorder.Drain();

        Assert.Equal(3, usage.Count);
        Assert.Equal(2, usage[new TenantUsageKey("tenant-1", Today, TenantUsageMeters.Requests)]);
        Assert.Equal(
            1,
            usage[new TenantUsageKey("tenant-1", Today, TenantUsageMeters.TokenRequests)]
        );
        Assert.Equal(1, usage[new TenantUsageKey("tenant-2", Today, TenantUsageMeters.Requests)]);
    }

    [Fact]
    public void Record_WhenTheUtcDayChanges_CountsTheNewDaySeparately()
    {
        var reads = 0;
        MockTimeProvider
            .Setup(x => x.GetUtcNow())
            .Returns(() => ++reads == 1 ? Now : Now.AddSeconds(1))
            .Verifiable(Times.Exactly(2));

        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);

        var usage = Recorder.Drain();

        Assert.Equal(1, usage[new TenantUsageKey("tenant-1", Today, TenantUsageMeters.Requests)]);
        Assert.Equal(
            1,
            usage[new TenantUsageKey("tenant-1", Today.AddDays(1), TenantUsageMeters.Requests)]
        );
    }

    [Theory]
    [InlineData("tenant-1", "")]
    [InlineData("", "requests")]
    [InlineData("tenant-1", null)]
    public void Record_WhenATenantOrMeterIsEmpty_Throws(string tenantId, string? meter)
    {
        Assert.ThrowsAny<ArgumentException>(() => Recorder.Record(tenantId, meter!));
    }

    [Fact]
    public void Record_WhenTheMeterIsTooLongToStore_Throws()
    {
        var meter = new string('m', OpenIdMaxLengths.UsageMeter + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => Recorder.Record("tenant-1", meter));
    }

    #endregion

    #region Drain Tests

    [Fact]
    public void Drain_TakesTheUsageOnce()
    {
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);

        Assert.Single(Recorder.Drain());
        Assert.Empty(Recorder.Drain());
    }

    #endregion
}
