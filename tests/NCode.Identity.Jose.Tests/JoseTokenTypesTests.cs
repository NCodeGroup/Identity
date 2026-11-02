// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.Tests;

public class JoseTokenTypesTests
{
    #region Normalize

    [Theory]
    [InlineData("at+jwt", "application/at+jwt")]
    [InlineData("JWT", "application/JWT")]
    [InlineData("application/at+jwt", "application/at+jwt")]
    [InlineData("text/plain", "text/plain")]
    public void Normalize_WithoutSlash_PrependsApplicationPrefix(string input, string expected)
    {
        var actual = JoseTokenTypes.Normalize(input);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Normalize_WhenNullOrEmpty_Throws(string? input)
    {
        Assert.ThrowsAny<ArgumentException>(() => JoseTokenTypes.Normalize(input!));
    }

    #endregion

    #region AreSame

    [Theory]
    [InlineData("at+jwt", "at+jwt")]
    [InlineData("at+jwt", "application/at+jwt")]
    [InlineData("application/at+jwt", "at+jwt")]
    [InlineData("AT+JWT", "at+jwt")]
    [InlineData("Application/AT+JWT", "application/at+jwt")]
    public void AreSame_WhenSameMediaType_ReturnsTrue(string left, string right)
    {
        Assert.True(JoseTokenTypes.AreSame(left, right));
    }

    [Theory]
    [InlineData("at+jwt", "dpop+jwt")]
    [InlineData("at+jwt", "JWT")]
    [InlineData("at+jwt", null)]
    [InlineData(null, "at+jwt")]
    [InlineData(null, null)]
    [InlineData("", "at+jwt")]
    public void AreSame_WhenDifferentOrMissing_ReturnsFalse(string? left, string? right)
    {
        Assert.False(JoseTokenTypes.AreSame(left, right));
    }

    #endregion
}
