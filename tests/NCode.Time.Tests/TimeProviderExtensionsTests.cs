// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Time.Testing;

namespace NCode.Time.Tests;

public sealed class TimeProviderExtensionsTests
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

    private FakeTimeProvider TimeProvider { get; } = new(UtcNow);

    #region GetUtcNowWithPrecisionInSeconds Tests

    [Fact]
    public void GetUtcNowWithPrecisionInSeconds_WhenNowHasFractionalSeconds_ReturnsTruncatedUtcNow()
    {
        var now = TimeProvider.GetUtcNowWithPrecisionInSeconds();

        Assert.Equal(UtcNowInSeconds, now);
        Assert.Equal(TimeSpan.Zero, now.Offset);
    }

    #endregion

    #region GetTimestampWithPrecisionInSeconds Tests

    [Fact]
    public void GetTimestampWithPrecisionInSeconds_WhenNowHasFractionalSeconds_ReturnsTruncatedUtcTicks()
    {
        var timestamp = TimeProvider.GetTimestampWithPrecisionInSeconds();

        Assert.Equal(UtcNowInSeconds.UtcTicks, timestamp);
    }

    #endregion
}
