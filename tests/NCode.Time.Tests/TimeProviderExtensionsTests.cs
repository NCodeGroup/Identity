// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Moq;

namespace NCode.Time.Tests;

public sealed class TimeProviderExtensionsTests : IDisposable
{
    private static readonly DateTimeOffset UtcNow = new DateTimeOffset(
        2026,
        10,
        20,
        13,
        45,
        30,
        TimeSpan.Zero
    ).AddTicks(1_234_567);

    private static readonly DateTimeOffset UtcNowInSeconds = new(
        2026,
        10,
        20,
        13,
        45,
        30,
        TimeSpan.Zero
    );

    private MockRepository MockRepository { get; } = new(MockBehavior.Strict);

    private Mock<TimeProvider> MockTimeProvider { get; }

    public TimeProviderExtensionsTests()
    {
        MockTimeProvider = MockRepository.Create<TimeProvider>();
        MockTimeProvider.Setup(x => x.GetUtcNow()).Returns(UtcNow).Verifiable();
    }

    public void Dispose() => MockRepository.Verify();

    #region GetUtcNowWithPrecisionInSeconds Tests

    [Fact]
    public void GetUtcNowWithPrecisionInSeconds_WhenNowHasFractionalSeconds_ReturnsTruncatedUtcNow()
    {
        var now = MockTimeProvider.Object.GetUtcNowWithPrecisionInSeconds();

        Assert.Equal(UtcNowInSeconds, now);
        Assert.Equal(TimeSpan.Zero, now.Offset);
    }

    #endregion

    #region GetTimestampWithPrecisionInSeconds Tests

    [Fact]
    public void GetTimestampWithPrecisionInSeconds_WhenNowHasFractionalSeconds_ReturnsTruncatedUtcTicks()
    {
        var timestamp = MockTimeProvider.Object.GetTimestampWithPrecisionInSeconds();

        Assert.Equal(UtcNowInSeconds.UtcTicks, timestamp);
    }

    #endregion
}
