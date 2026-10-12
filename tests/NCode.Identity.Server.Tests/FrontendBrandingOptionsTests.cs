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
