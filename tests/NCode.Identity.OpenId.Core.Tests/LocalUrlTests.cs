#region Copyright Preamble
//
//    Copyright @ 2026 NCode Group
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


using Xunit;

namespace NCode.Identity.OpenId.Core.Tests;

public class LocalUrlTests
{
    #region IsLocal Tests

    [Theory]
    [InlineData("/")]
    [InlineData("/a")]
    [InlineData("/a/b?c=%2F")]
    [InlineData("/a#f")]
    public void IsLocal_LocalPath_ReturnsTrue(string value)
    {
        Assert.True(LocalUrl.IsLocal(value));
        Assert.True(LocalUrl.IsLocal(value.AsSpan()));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("/\t/evil.example")]
    [InlineData("/\r\n/evil.example")]
    [InlineData("/a b")]
    [InlineData("/a\\b")]
    [InlineData("https://evil.example/")]
    [InlineData("/a\u0000")]
    public void IsLocal_NotLocal_ReturnsFalse(string? value)
    {
        Assert.False(LocalUrl.IsLocal(value));
        Assert.False(LocalUrl.IsLocal(value.AsSpan()));
    }

    #endregion
}
