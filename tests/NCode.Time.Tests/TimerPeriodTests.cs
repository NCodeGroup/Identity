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

namespace NCode.Time.Tests;

public class TimerPeriodTests
{
    #region IsValid Tests

    [Fact]
    public void IsValid_WhenAtEitherBound_ReturnsTrue()
    {
        Assert.True(TimerPeriod.IsValid(TimerPeriod.MinValue));
        Assert.True(TimerPeriod.IsValid(TimerPeriod.MaxValue));
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    public void IsValid_WhenZeroOrNegative_ReturnsFalse(long ticks)
    {
        Assert.False(TimerPeriod.IsValid(TimeSpan.FromTicks(ticks)));
    }

    [Fact]
    public void IsValid_WhenShorterThanOneMillisecond_ReturnsFalse()
    {
        Assert.False(TimerPeriod.IsValid(TimeSpan.FromTicks(5_000)));
    }

    [Fact]
    public void IsValid_WhenLongerThanTheMaximum_ReturnsFalse()
    {
        Assert.False(TimerPeriod.IsValid(TimerPeriod.MaxValue + TimeSpan.FromMilliseconds(1)));
    }

    #endregion

    #region MinValue Tests

    [Fact]
    public void MinValue_IsTheShortestPeriodAPeriodicTimerAccepts()
    {
        using var timer = new PeriodicTimer(TimerPeriod.MinValue, TimeProvider.System);

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PeriodicTimer(TimerPeriod.MinValue - TimeSpan.FromTicks(1), TimeProvider.System)
        );
    }

    #endregion

    #region MaxValue Tests

    [Fact]
    public void MaxValue_IsTheLongestPeriodATimerAccepts()
    {
        using var periodicTimer = new PeriodicTimer(TimerPeriod.MaxValue, TimeProvider.System);
        using var timer = TimeProvider.System.CreateTimer(
            _ => { },
            null,
            TimerPeriod.MaxValue,
            TimerPeriod.MaxValue
        );

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new PeriodicTimer(
                TimerPeriod.MaxValue + TimeSpan.FromMilliseconds(1),
                TimeProvider.System
            )
        );
    }

    #endregion
}
