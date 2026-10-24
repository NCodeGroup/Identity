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
