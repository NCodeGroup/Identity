#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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

namespace NCode.Identity.Models;

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
