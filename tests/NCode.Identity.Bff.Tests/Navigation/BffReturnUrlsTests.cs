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

using NCode.Identity.Bff.Navigation;
using Xunit;

namespace NCode.Identity.Bff.Tests.Navigation;

public class BffReturnUrlsTests : BaseTests
{
    #region IsAllowed Tests

    [Theory]
    [InlineData("/orders")]
    [InlineData("/orders/1?x=1")]
    [InlineData("/orders?x=1")]
    public void IsAllowed_WhenUnderAnEntry_ReturnsTrue(string returnUrl)
    {
        Assert.True(BffReturnUrls.IsAllowed(returnUrl, ["/orders"]));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/orders")]
    [InlineData("/orders/1?x=1")]
    public void IsAllowed_WhenTheEntryIsTheRoot_ReturnsTrueForWellFormedPaths(string returnUrl)
    {
        Assert.True(BffReturnUrls.IsAllowed(returnUrl, ["/"]));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/ordersx")]
    [InlineData("//evil.com")]
    [InlineData("/\\evil")]
    [InlineData("/a/../b")]
    [InlineData("/%2e%2e/x")]
    [InlineData("https://x/")]
    [InlineData("orders")]
    [InlineData("/a#b")]
    public void IsAllowed_WhenRefused_ReturnsFalse(string returnUrl)
    {
        Assert.False(BffReturnUrls.IsAllowed(returnUrl, ["/orders"]));
    }

    [Theory]
    [InlineData("//evil.com")]
    [InlineData("/\\evil")]
    [InlineData("/a/../b")]
    [InlineData("https://x/")]
    [InlineData("orders")]
    [InlineData("/a#b")]
    public void IsAllowed_WhenTheEntryIsTheRootButThePathIsMalformed_ReturnsFalse(string returnUrl)
    {
        Assert.False(BffReturnUrls.IsAllowed(returnUrl, ["/"]));
    }

    #endregion

    #region IsWellFormedPath Tests

    [Theory]
    [InlineData("/")]
    [InlineData("/a/b?x=1")]
    public void IsWellFormedPath_WhenWellFormed_ReturnsTrue(string value)
    {
        Assert.True(BffReturnUrls.IsWellFormedPath(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("/a b")]
    [InlineData("/\t/evil.example")]
    [InlineData("/a\u0000")]
    [InlineData("/a%2Fb")]
    [InlineData("/a%5Cb")]
    [InlineData("/a/./b")]
    public void IsWellFormedPath_WhenMalformed_ReturnsFalse(string value)
    {
        Assert.False(BffReturnUrls.IsWellFormedPath(value));
    }

    #endregion
}
