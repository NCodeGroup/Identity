// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.Server.Frontend;
using Xunit;

namespace NCode.Identity.Server.Tests;

public class FrontendBrandingOptionsTests
{
    #region IsEmpty Tests

    [Fact]
    public void IsEmpty_WhenAllTokensUnset_IsTrue()
    {
        var options = new FrontendBrandingOptions();

        Assert.True(options.IsEmpty);
    }

    [Fact]
    public void IsEmpty_WhenAllTokensWhitespace_IsTrue()
    {
        var options = new FrontendBrandingOptions
        {
            DisplayName = "  ",
            LogoUrl = "",
            PrimaryColor = null,
            SecondaryColor = " ",
            Mode = "",
        };

        Assert.True(options.IsEmpty);
    }

    [Fact]
    public void IsEmpty_WhenAnyTokenSet_IsFalse()
    {
        var options = new FrontendBrandingOptions { Mode = "dark" };

        Assert.False(options.IsEmpty);
    }

    #endregion
}
