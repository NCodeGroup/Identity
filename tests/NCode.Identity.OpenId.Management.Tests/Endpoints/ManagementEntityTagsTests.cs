// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.OpenId.Management.Endpoints;

public class ManagementEntityTagsTests
{
    private const string ConcurrencyToken = "abc";
    private const string Resource = "settings";

    #region Format Tests

    [Fact]
    public void Format_Always_ReturnsTheQuotedStrongTag()
    {
        var result = ManagementEntityTags.Format(ConcurrencyToken);

        Assert.Equal("\"abc\"", result);
    }

    [Fact]
    public void Format_WhenTheTokenHasAQuote_Throws()
    {
        Assert.Throws<FormatException>(() => ManagementEntityTags.Format("a\"b"));
    }

    #endregion

    #region MatchesIfNoneMatch Tests

    [Theory]
    [InlineData("\"abc\"")]
    [InlineData("W/\"abc\"")]
    [InlineData("\"x\", \"abc\"")]
    [InlineData("*")]
    public void MatchesIfNoneMatch_WhenTheHeaderNamesTheTag_ReturnsTrue(string ifNoneMatch)
    {
        var result = ManagementEntityTags.MatchesIfNoneMatch(ifNoneMatch, ConcurrencyToken);

        Assert.True(result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("\"x\"")]
    [InlineData("abc")]
    [InlineData("\"abc")]
    public void MatchesIfNoneMatch_WhenTheHeaderDoesNotNameTheTag_ReturnsFalse(string? ifNoneMatch)
    {
        var result = ManagementEntityTags.MatchesIfNoneMatch(ifNoneMatch, ConcurrencyToken);

        Assert.False(result);
    }

    #endregion

    #region GetIfMatchErrorOrDefault Tests

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("*")]
    [InlineData("\"abc\"")]
    [InlineData("\"x\", \"abc\"")]
    [InlineData("abc")]
    public void GetIfMatchErrorOrDefault_WhenTheHeaderMatches_ReturnsNull(string? ifMatch)
    {
        var result = ManagementEntityTags.GetIfMatchErrorOrDefault(
            ifMatch,
            ConcurrencyToken,
            Resource
        );

        Assert.Null(result);
    }

    [Theory]
    [InlineData("\"x\"")]
    [InlineData("W/\"abc\"")]
    [InlineData("stale")]
    public void GetIfMatchErrorOrDefault_WhenTheTagDoesNotMatch_Returns412NamingTheResource(
        string ifMatch
    )
    {
        var result = ManagementEntityTags.GetIfMatchErrorOrDefault(
            ifMatch,
            ConcurrencyToken,
            Resource
        );

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, result.StatusCode);
        Assert.Equal(
            "The supplied If-Match entity tag does not match the current settings.",
            result.Detail
        );
    }

    [Theory]
    [InlineData("\"abc")]
    [InlineData("abc, def")]
    [InlineData("a b")]
    public void GetIfMatchErrorOrDefault_WhenTheHeaderIsMalformed_Returns412NamingTheAcceptedForms(
        string ifMatch
    )
    {
        var result = ManagementEntityTags.GetIfMatchErrorOrDefault(
            ifMatch,
            ConcurrencyToken,
            Resource
        );

        Assert.NotNull(result);
        Assert.Equal(StatusCodes.Status412PreconditionFailed, result.StatusCode);
        Assert.Equal(
            "The If-Match header must be \"*\", a list of entity tags such as \"\\\"<concurrency token>\\\"\", or the resource's concurrencyToken.",
            result.Detail
        );
    }

    #endregion
}
