// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Bff.Frontends;
using Xunit;

namespace NCode.Identity.Bff.Tests.Frontends;

public class BffFrontendNamesTests : BaseTests
{
    #region IsValid Tests

    [Theory]
    [InlineData("a")]
    [InlineData("shop")]
    [InlineData("admin-2")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void IsValid_ValidName_ReturnsTrue(string name)
    {
        Assert.True(BffFrontendNames.IsValid(name));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    [InlineData("Shop")]
    [InlineData("-a")]
    [InlineData("a-")]
    [InlineData("a_b")]
    [InlineData("a.b")]
    public void IsValid_InvalidName_ReturnsFalse(string? name)
    {
        Assert.False(BffFrontendNames.IsValid(name));
    }

    #endregion
}
