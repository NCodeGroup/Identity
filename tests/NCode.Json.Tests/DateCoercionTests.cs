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

namespace NCode.Json.Tests;

public class DateCoercionTests
{
    // Inclusive Unix-second bounds of DateTimeOffset.MinValue/MaxValue.
    private const long MinSeconds = -62_135_596_800;
    private const long MaxSeconds = 253_402_300_799;

    #region TryFromNumericDate Tests

    [Theory]
    [InlineData(0L)]
    [InlineData(1_700_000_000L)]
    [InlineData(MinSeconds)]
    [InlineData(MaxSeconds)]
    public void TryFromNumericDate_WhenSecondsInRange_ReturnsUtcInstant(long seconds)
    {
        var converted = DateCoercion.TryFromNumericDate(seconds, out var value);

        Assert.True(converted);
        Assert.Equal(TimeSpan.Zero, value.Offset);
        Assert.Equal(seconds, value.ToUnixTimeSeconds());
    }

    [Theory]
    [InlineData(MinSeconds - 1)]
    [InlineData(MaxSeconds + 1)]
    [InlineData(long.MinValue)]
    [InlineData(long.MaxValue)]
    public void TryFromNumericDate_WhenSecondsOutOfRange_ReturnsFalse(long seconds)
    {
        var converted = DateCoercion.TryFromNumericDate(seconds, out _);

        Assert.False(converted);
    }

    [Fact]
    public void TryFromNumericDate_WhenFractionalSeconds_KeepsFraction()
    {
        var converted = DateCoercion.TryFromNumericDate(1_700_000_000.25, out var value);

        Assert.True(converted);
        Assert.Equal(
            DateTimeOffset.UnixEpoch.AddSeconds(1_700_000_000).AddMilliseconds(250),
            value
        );
    }

    [Fact]
    public void TryFromNumericDate_WhenNegativeFractionalSeconds_FloorsToPriorSecond()
    {
        var converted = DateCoercion.TryFromNumericDate(-0.5, out var value);

        Assert.True(converted);
        Assert.Equal(DateTimeOffset.UnixEpoch.AddMilliseconds(-500), value);
    }

    [Fact]
    public void TryFromNumericDate_WhenFractionalAtMaxSecond_ReturnsMaxRange()
    {
        var converted = DateCoercion.TryFromNumericDate(MaxSeconds + 0.5, out var value);

        Assert.True(converted);
        Assert.Equal(MaxSeconds, value.ToUnixTimeSeconds());
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1e20)]
    [InlineData(-1e20)]
    [InlineData(MaxSeconds + 1.0)]
    [InlineData(MinSeconds - 0.5)]
    public void TryFromNumericDate_WhenFractionalNotFiniteOrOutOfRange_ReturnsFalse(double seconds)
    {
        var converted = DateCoercion.TryFromNumericDate(seconds, out _);

        Assert.False(converted);
    }

    #endregion

    #region TryParseNumericDate Tests

    [Theory]
    [InlineData("1700000000", 1_700_000_000L)]
    [InlineData("-1", -1L)]
    [InlineData("1.7e9", 1_700_000_000L)]
    [InlineData("1700000000.75", 1_700_000_000L)]
    public void TryParseNumericDate_WhenNumber_ReturnsInstant(string text, long expectedSeconds)
    {
        var parsed = DateCoercion.TryParseNumericDate(text, out var value);

        Assert.True(parsed);
        Assert.Equal(expectedSeconds, value.ToUnixTimeSeconds());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" 1700000000")]
    [InlineData("1,700,000,000")]
    [InlineData("2024-01-02T03:04:05Z")]
    [InlineData("NaN")]
    [InlineData("99999999999999")]
    [InlineData("99999999999999999999")]
    [InlineData("1e400")]
    public void TryParseNumericDate_WhenNotANumericDate_ReturnsFalse(string? text)
    {
        var parsed = DateCoercion.TryParseNumericDate(text, out _);

        Assert.False(parsed);
    }

    #endregion

    #region TryParseDateTimeOffset Tests

    [Fact]
    public void TryParseDateTimeOffset_WhenZoneless_AssumesUtc()
    {
        var parsed = DateCoercion.TryParseDateTimeOffset("2024-01-02T03:04:05", out var value);

        Assert.True(parsed);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), value);
        Assert.Equal(TimeSpan.Zero, value.Offset);
    }

    [Fact]
    public void TryParseDateTimeOffset_WhenExplicitOffset_PreservesOffset()
    {
        var parsed = DateCoercion.TryParseDateTimeOffset(
            "2024-01-02T03:04:05+05:00",
            out var value
        );

        Assert.True(parsed);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.FromHours(5)), value);
        Assert.Equal(TimeSpan.FromHours(5), value.Offset);
    }

    [Fact]
    public void TryParseDateTimeOffset_WhenInstantOutOfRange_ReturnsFalse()
    {
        var parsed = DateCoercion.TryParseDateTimeOffset("0001-01-01T00:00:00+05:00", out _);

        Assert.False(parsed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    public void TryParseDateTimeOffset_WhenNullOrUnparseable_ReturnsFalse(string? text)
    {
        var parsed = DateCoercion.TryParseDateTimeOffset(text, out _);

        Assert.False(parsed);
    }

    #endregion

    #region TryParseDateTime Tests

    [Fact]
    public void TryParseDateTime_WhenUtcDesignator_ReturnsUtcKind()
    {
        var parsed = DateCoercion.TryParseDateTime("2024-01-02T03:04:05Z", out var value);

        Assert.True(parsed);
        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), value);
    }

    [Fact]
    public void TryParseDateTime_WhenZoneless_LabelsUtcKindWithoutShift()
    {
        var parsed = DateCoercion.TryParseDateTime("2024-01-02T03:04:05", out var value);

        Assert.True(parsed);
        Assert.Equal(DateTimeKind.Utc, value.Kind);
        Assert.Equal(new DateTime(2024, 1, 2, 3, 4, 5, DateTimeKind.Utc), value);
    }

    [Fact]
    public void TryParseDateTime_WhenExplicitOffset_ReturnsLocalKindOfSameInstant()
    {
        var parsed = DateCoercion.TryParseDateTime("2024-01-02T03:04:05+05:00", out var value);

        Assert.True(parsed);
        Assert.Equal(DateTimeKind.Local, value.Kind);
        Assert.Equal(new DateTime(2024, 1, 1, 22, 4, 5, DateTimeKind.Utc), value.ToUniversalTime());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    public void TryParseDateTime_WhenNullOrUnparseable_ReturnsFalse(string? text)
    {
        var parsed = DateCoercion.TryParseDateTime(text, out _);

        Assert.False(parsed);
    }

    #endregion
}
