// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace NCode.Identity.Bff.Tests;

public class BffFrontendAttributeTests : BaseTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithName_KeepsIt()
    {
        var attribute = new BffFrontendAttribute("admin");

        Assert.Equal("admin", attribute.Name);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Constructor_WithBlankName_Throws(string name)
    {
        Assert.Throws<ArgumentException>(() => new BffFrontendAttribute(name));
    }

    #endregion
}
