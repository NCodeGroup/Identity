// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.SdJwt.Tests;

public class JsonPointerTests
{
    #region AppendMember Tests

    [Theory]
    [InlineData("", "name", "/name")]
    [InlineData("/address", "street", "/address/street")]
    [InlineData("", "a/b", "/a~1b")]
    [InlineData("", "a~b", "/a~0b")]
    [InlineData("", "", "/")]
    public void AppendMember_EscapesPerRfc6901(string pointer, string member, string expected)
    {
        Assert.Equal(expected, JsonPointer.AppendMember(pointer, member));
    }

    #endregion

    #region AppendIndex Tests

    [Fact]
    public void AppendIndex_WritesInvariantIndex()
    {
        Assert.Equal("/nationalities/10", JsonPointer.AppendIndex("/nationalities", 10));
    }

    #endregion

    #region IsWellFormed Tests

    [Theory]
    [InlineData("/a", true)]
    [InlineData("/a/0", true)]
    [InlineData("/a~1b/c~0d", true)]
    [InlineData("", false)]
    [InlineData("a", false)]
    [InlineData("/a~", false)]
    [InlineData("/a~2", false)]
    public void IsWellFormed_JudgesSyntax(string pointer, bool expected)
    {
        Assert.Equal(expected, JsonPointer.IsWellFormed(pointer));
    }

    #endregion

    #region GetFirstToken Tests

    [Theory]
    [InlineData("/iss", "iss")]
    [InlineData("/address/street", "address")]
    [InlineData("/a~1b/c", "a/b")]
    [InlineData("/a~0b", "a~b")]
    public void GetFirstToken_UnescapesTopLevelName(string pointer, string expected)
    {
        Assert.Equal(expected, JsonPointer.GetFirstToken(pointer));
    }

    #endregion
}
