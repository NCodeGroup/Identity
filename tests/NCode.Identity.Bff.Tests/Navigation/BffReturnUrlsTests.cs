// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
