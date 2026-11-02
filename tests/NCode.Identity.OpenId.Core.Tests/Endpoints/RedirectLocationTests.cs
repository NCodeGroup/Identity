// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.Endpoints;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints;

public class RedirectLocationTests : BaseTests
{
    #region Escape Tests

    [Fact]
    public void Escape_WhenEveryCharacterIsPrintableAscii_ReturnsItAsWritten()
    {
        const string location = "https://client.example/cb?a=b#_=_";

        var actual = RedirectLocation.Escape(location);

        Assert.Equal(location, actual);
    }

    [Fact]
    public void Escape_WhenTheLocationHasANonAsciiCharacter_PercentEncodesItsUtf8()
    {
        var actual = RedirectLocation.Escape("https://client.example/caf\u00e9");

        Assert.Equal("https://client.example/caf%C3%A9", actual);
    }

    [Theory]
    [InlineData("\r\n", "%0D%0A")]
    [InlineData(" ", "%20")]
    [InlineData("\u007f", "%7F")]
    public void Escape_WhenTheLocationHasAControlCharacterOrSpace_PercentEncodesIt(
        string unsafeCharacters,
        string expected
    )
    {
        var actual = RedirectLocation.Escape("https://client.example/cb" + unsafeCharacters);

        Assert.Equal("https://client.example/cb" + expected, actual);
    }

    #endregion
}
