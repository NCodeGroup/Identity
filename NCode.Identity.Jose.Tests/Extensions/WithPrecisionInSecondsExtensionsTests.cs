#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using NCode.Identity.Jose.Extensions;

namespace NCode.Jose.Tests.Extensions;

public class WithPrecisionInSecondsExtensionsTests : BaseTests
{
    public static TheoryData<DateTime, DateTimeKind> DateTimeWithPrecisionInSecondsTestData => new()
    {
        { DateTime.Now, DateTimeKind.Local },
        { DateTime.UtcNow, DateTimeKind.Utc },
        { DateTime.SpecifyKind(DateTime.Now, DateTimeKind.Unspecified), DateTimeKind.Unspecified },
    };

    [Theory]
    [MemberData(nameof(DateTimeWithPrecisionInSecondsTestData))]
    public void DateTime_WithPrecisionInSeconds(DateTime value, DateTimeKind kind)
    {
        Assert.Equal(kind, value.Kind);

        value = value.AddMilliseconds(-value.Millisecond);
        Assert.Equal(0.0, value.Millisecond);

        value = value.AddMilliseconds(100.0);
        Assert.Equal(100.0, value.Millisecond);

        var newValue = value.WithPrecisionInSeconds();
        Assert.Equal(kind, newValue.Kind);
        Assert.Equal(value.Year, newValue.Year);
        Assert.Equal(value.Month, newValue.Month);
        Assert.Equal(value.Day, newValue.Day);
        Assert.Equal(value.Hour, newValue.Hour);
        Assert.Equal(value.Minute, newValue.Minute);
        Assert.Equal(value.Second, newValue.Second);
        Assert.Equal(0.0, newValue.Millisecond);
    }

    public static TheoryData<DateTimeOffset> DateTimeOffsetWithPrecisionInSecondsTestData => new()
    {
        DateTimeOffset.Now,
        DateTimeOffset.UtcNow,
    };

    [Theory]
    [MemberData(nameof(DateTimeOffsetWithPrecisionInSecondsTestData))]
    public void DateTimeOffset_WithPrecisionInSeconds(DateTimeOffset value)
    {
        value = value.AddMilliseconds(-value.Millisecond);
        Assert.Equal(0.0, value.Millisecond);

        value = value.AddMilliseconds(100.0);
        Assert.Equal(100.0, value.Millisecond);

        var newValue = value.WithPrecisionInSeconds();
        Assert.Equal(value.Offset, newValue.Offset);
        Assert.Equal(value.Year, newValue.Year);
        Assert.Equal(value.Month, newValue.Month);
        Assert.Equal(value.Day, newValue.Day);
        Assert.Equal(value.Hour, newValue.Hour);
        Assert.Equal(value.Minute, newValue.Minute);
        Assert.Equal(value.Second, newValue.Second);
        Assert.Equal(0.0, newValue.Millisecond);
    }

    [Fact]
    private void GetTimestampWithPrecisionInSeconds()
    {
        var mockTimeProvider = CreateStrictMock<TimeProvider>();

        var utcNow = DateTimeOffset.UtcNow;
        Assert.NotEqual(0.0, utcNow.Millisecond);

        mockTimeProvider
            .Setup(x => x.GetTimestamp())
            .Returns(utcNow.Ticks)
            .Verifiable();

        mockTimeProvider
            .SetupGet(x => x.TimestampFrequency)
            .Returns(TimeSpan.TicksPerSecond)
            .Verifiable();

        var expected = utcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond;
        var timestampWithPrecisionInSeconds = mockTimeProvider.Object.GetTimestampWithPrecisionInSeconds();
        Assert.Equal(expected, timestampWithPrecisionInSeconds);

        var dateTime = new DateTime(timestampWithPrecisionInSeconds, DateTimeKind.Utc);
        Assert.Equal(DateTimeKind.Utc, dateTime.Kind);
        Assert.Equal(0.0, dateTime.Millisecond);
    }

    [Fact]
    private void GetUtcNowWithPrecisionInSeconds()
    {
        var mockTimeProvider = CreateStrictMock<TimeProvider>();

        var utcNow = DateTimeOffset.UtcNow;
        Assert.NotEqual(0.0, utcNow.Millisecond);

        mockTimeProvider
            .Setup(x => x.GetTimestamp())
            .Returns(utcNow.Ticks)
            .Verifiable();

        mockTimeProvider
            .SetupGet(x => x.TimestampFrequency)
            .Returns(TimeSpan.TicksPerSecond)
            .Verifiable();

        var utcNowWithPrecisionInSeconds = mockTimeProvider.Object.GetUtcNowWithPrecisionInSeconds();
        Assert.Equal(TimeSpan.Zero, utcNowWithPrecisionInSeconds.Offset);
        Assert.Equal(0.0, utcNowWithPrecisionInSeconds.Millisecond);
    }
}
