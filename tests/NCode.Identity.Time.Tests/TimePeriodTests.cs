// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Time.Tests;

public class TimePeriodTests
{
    #region Duration Property Tests

    [Fact]
    public void Duration_WhenEndTimeIsSet_ReturnsDifference()
    {
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddHours(3);
        var period = new TimePeriod { StartTime = start, EndTime = end };

        Assert.Equal(TimeSpan.FromHours(3), period.Duration);
    }

    [Fact]
    public void Duration_WhenEndTimeIsNull_ReturnsNull()
    {
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var period = new TimePeriod { StartTime = start, EndTime = null };

        Assert.Null(period.Duration);
    }

    #endregion

    #region Property Tests

    [Fact]
    public void StartTime_WhenSet_RoundTrips()
    {
        var start = new DateTimeOffset(2025, 6, 15, 12, 30, 0, TimeSpan.Zero);

        var period = new TimePeriod { StartTime = start };

        Assert.Equal(start, period.StartTime);
        Assert.Null(period.EndTime);
    }

    [Fact]
    public void Equality_WhenSameValues_AreEqual()
    {
        var start = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var end = start.AddDays(1);

        var left = new TimePeriod { StartTime = start, EndTime = end };
        var right = new TimePeriod { StartTime = start, EndTime = end };

        Assert.Equal(left, right);
    }

    #endregion
}
