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
