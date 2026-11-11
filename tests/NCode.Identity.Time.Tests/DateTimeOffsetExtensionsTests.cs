// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Time.Tests;

public class DateTimeOffsetExtensionsTests
{
    #region WithPrecisionInSeconds Tests

    [Theory]
    [InlineData(0)]
    [InlineData(330)]
    [InlineData(-480)]
    public void WithPrecisionInSeconds_WhenFractionalSeconds_TruncatesAndKeepsOffset(
        int offsetMinutes
    )
    {
        var offset = TimeSpan.FromMinutes(offsetMinutes);
        var value = new DateTimeOffset(2026, 10, 20, 13, 45, 30, 999, offset).AddTicks(9_999);

        var truncated = value.WithPrecisionInSeconds();

        Assert.Equal(new DateTimeOffset(2026, 10, 20, 13, 45, 30, offset), truncated);
        Assert.Equal(offset, truncated.Offset);
    }

    [Fact]
    public void WithPrecisionInSeconds_WhenWholeSeconds_ReturnsSameValue()
    {
        var value = new DateTimeOffset(2026, 10, 20, 13, 45, 30, TimeSpan.Zero);

        var truncated = value.WithPrecisionInSeconds();

        Assert.Equal(value, truncated);
    }

    #endregion
}
