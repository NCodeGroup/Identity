// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
