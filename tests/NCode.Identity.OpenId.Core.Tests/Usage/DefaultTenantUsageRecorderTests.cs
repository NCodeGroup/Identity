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
        MockTimeProvider.SetupSequence(x => x.GetUtcNow()).Returns(Now).Returns(Now.AddSeconds(1));

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

    #region Restore Tests

    [Fact]
    public void Restore_AddsTheUsageToWhatWasRecordedSince()
    {
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(Now).Verifiable();
        var key = new TenantUsageKey("tenant-1", Today, TenantUsageMeters.Requests);
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);
        var drained = Recorder.Drain();
        Recorder.Record("tenant-1", TenantUsageMeters.Requests);

        Recorder.Restore(drained);

        Assert.Equal(2, Recorder.Drain()[key]);
    }

    #endregion
}
