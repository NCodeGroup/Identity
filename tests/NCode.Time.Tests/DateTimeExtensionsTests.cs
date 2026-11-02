// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Time.Tests;

public class DateTimeExtensionsTests
{
    #region WithPrecisionInSeconds Tests

    [Theory]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void WithPrecisionInSeconds_WhenFractionalSeconds_TruncatesAndKeepsKind(
        DateTimeKind kind
    )
    {
        var value = new DateTime(2026, 10, 20, 13, 45, 30, 999, kind).AddTicks(9_999);

        var truncated = value.WithPrecisionInSeconds();

        Assert.Equal(new DateTime(2026, 10, 20, 13, 45, 30, kind), truncated);
        Assert.Equal(kind, truncated.Kind);
    }

    [Fact]
    public void WithPrecisionInSeconds_WhenWholeSeconds_ReturnsSameValue()
    {
        var value = new DateTime(2026, 10, 20, 13, 45, 30, DateTimeKind.Utc);

        var truncated = value.WithPrecisionInSeconds();

        Assert.Equal(value, truncated);
    }

    #endregion
}
