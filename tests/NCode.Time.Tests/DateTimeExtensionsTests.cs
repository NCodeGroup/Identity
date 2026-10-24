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
