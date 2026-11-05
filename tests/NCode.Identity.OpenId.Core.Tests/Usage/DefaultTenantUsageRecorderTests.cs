// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Time.Testing;
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Persistence;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Usage;

public sealed class DefaultTenantUsageRecorderTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 23, 59, 59, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 10, 21);

    private FakeTimeProvider TimeProvider { get; } = new(Now);
    private DefaultTenantUsageRecorder Recorder { get; }

    public DefaultTenantUsageRecorderTests()
    {
        Recorder = new DefaultTenantUsageRecorder(TimeProvider);
    }

    #region Record Tests

    [Fact]
    public void Record_CountsEachTenantDayAndMeter()
    {
        Recorder.Record("team-1", "tenant-1", TenantUsageMeters.Requests);
        Recorder.Record("team-1", "tenant-1", TenantUsageMeters.Requests);
        Recorder.Record("team-1", "tenant-1", TenantUsageMeters.TokenRequests);
        Recorder.Record("team-1", "tenant-2", TenantUsageMeters.Requests);

        var usage = Recorder.Drain();

        Assert.Equal(3, usage.Count);
        Assert.Equal(
            2,
            usage[new TenantUsageKey("team-1", "tenant-1", Today, TenantUsageMeters.Requests)]
        );
        Assert.Equal(
            1,
            usage[new TenantUsageKey("team-1", "tenant-1", Today, TenantUsageMeters.TokenRequests)]
        );
        Assert.Equal(
            1,
            usage[new TenantUsageKey("team-1", "tenant-2", Today, TenantUsageMeters.Requests)]
        );
    }

    [Fact]
    public void Record_WhenTheUtcDayChanges_CountsTheNewDaySeparately()
    {
        Recorder.Record("team-1", "tenant-1", TenantUsageMeters.Requests);
        TimeProvider.Advance(TimeSpan.FromSeconds(1));
        Recorder.Record("team-1", "tenant-1", TenantUsageMeters.Requests);

        var usage = Recorder.Drain();

        Assert.Equal(
            1,
            usage[new TenantUsageKey("team-1", "tenant-1", Today, TenantUsageMeters.Requests)]
        );
        Assert.Equal(
            1,
            usage[
                new TenantUsageKey(
                    "team-1",
                    "tenant-1",
                    Today.AddDays(1),
                    TenantUsageMeters.Requests
                )
            ]
        );
    }

    [Theory]
    [InlineData("tenant-1", "")]
    [InlineData("", "requests")]
    [InlineData("tenant-1", null)]
    public void Record_WhenATenantOrMeterIsEmpty_Throws(string tenantId, string? meter)
    {
        Assert.ThrowsAny<ArgumentException>(() => Recorder.Record("team-1", tenantId, meter!));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Record_WhenTheTeamIsEmpty_Throws(string? teamId)
    {
        Assert.ThrowsAny<ArgumentException>(() =>
            Recorder.Record(teamId!, "tenant-1", TenantUsageMeters.Requests)
        );
    }

    [Fact]
    public void Record_WhenTheMeterIsTooLongToStore_Throws()
    {
        var meter = new string('m', OpenIdMaxLengths.UsageMeter + 1);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Recorder.Record("team-1", "tenant-1", meter)
        );
    }

    #endregion

    #region Drain Tests

    [Fact]
    public void Drain_TakesTheUsageOnce()
    {
        Recorder.Record("team-1", "tenant-1", TenantUsageMeters.Requests);

        Assert.Single(Recorder.Drain());
        Assert.Empty(Recorder.Drain());
    }

    #endregion
}
