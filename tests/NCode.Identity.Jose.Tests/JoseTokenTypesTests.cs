#region Copyright Preamble

//
//    Copyright @ 2025 NCode Group
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
