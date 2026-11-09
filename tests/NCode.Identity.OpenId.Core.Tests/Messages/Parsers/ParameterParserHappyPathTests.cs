// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parsers;

public class ParameterParserHappyPathTests : BaseTests
{
    private OpenIdEnvironment Environment { get; }
    private ParameterDescriptor AllowMissingDescriptor { get; } =
        new("test", ParameterLoader.Default);

    public ParameterParserHappyPathTests()
    {
        // The happy paths under test never touch the environment; a strict mock guards that.
        Environment = CreateStrictMock<OpenIdEnvironment>().Object;
    }

    #region BoolParser Tests

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void BoolParser_GetStringValues_ReturnsExpected(bool value, string expected)
    {
        var parser = new BoolParser();

        var result = parser.GetStringValues(Environment, AllowMissingDescriptor, value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("TRUE", true)]
    public void BoolParser_Parse_ReturnsExpected(string input, bool expected)
    {
        var parser = new BoolParser();

        var result = parser.Parse(Environment, AllowMissingDescriptor, input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BoolParser_Parse_WhenMissingAndAllowed_ReturnsFalse()
    {
        var parser = new BoolParser();

        var result = parser.Parse(Environment, AllowMissingDescriptor, StringValues.Empty);

        Assert.False(result);
    }

    #endregion

    #region StringValuesParser Tests

    [Fact]
    public void StringValuesParser_GetStringValues_ReturnsSameValue()
    {
        var parser = new StringValuesParser();
        var value = new StringValues(["a", "b"]);

        var result = parser.GetStringValues(Environment, AllowMissingDescriptor, value);

        Assert.Equal(value, result);
    }

    [Fact]
    public void StringValuesParser_Parse_ReturnsSameValue()
    {
        var parser = new StringValuesParser();
        var value = new StringValues(["a", "b"]);

        var result = parser.Parse(Environment, AllowMissingDescriptor, value);

        Assert.Equal(value, result);
    }

    [Fact]
    public void StringValuesParser_Parse_WhenMissingAndAllowed_ReturnsEmpty()
    {
        var parser = new StringValuesParser();

        var result = parser.Parse(Environment, AllowMissingDescriptor, StringValues.Empty);

        Assert.Equal(StringValues.Empty, result);
    }

    #endregion

    #region StringSetParser Tests

    [Fact]
    public void StringSetParser_GetStringValues_WhenNull_ReturnsEmpty()
    {
        var parser = new StringSetParser();

        var result = parser.GetStringValues(Environment, AllowMissingDescriptor, null);

        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void StringSetParser_GetStringValues_WhenEmpty_ReturnsEmpty()
    {
        var parser = new StringSetParser();

        var result = parser.GetStringValues(Environment, AllowMissingDescriptor, []);

        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void StringSetParser_GetStringValues_WhenSorted_ReturnsOrdered()
    {
        var parser = new StringSetParser();
        var knownParameter = new KnownParameter<HashSet<string>>("test", parser)
        {
            AllowMissingStringValues = true,
            SortStringValues = true,
        };
        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.GetStringValues(Environment, descriptor, ["c", "a", "b"]);

        Assert.Equal(new StringValues(["a", "b", "c"]), result);
    }

    [Fact]
    public void StringSetParser_Parse_SplitsOnSeparator()
    {
        var parser = new StringSetParser();
        var input =
            $"a{OpenIdProtocol.ParameterSeparatorChar}b{OpenIdProtocol.ParameterSeparatorChar}c";

        var result = parser.Parse(Environment, AllowMissingDescriptor, input);

        Assert.NotNull(result);
        Assert.Equal(["a", "b", "c"], result.Order());
    }

    [Fact]
    public void StringSetParser_Parse_WhenMissingAndAllowed_ReturnsNull()
    {
        var parser = new StringSetParser();

        var result = parser.Parse(Environment, AllowMissingDescriptor, StringValues.Empty);

        Assert.Null(result);
    }

    [Fact]
    public void StringSetParser_Clone_CreatesIndependentCopy()
    {
        var parser = new StringSetParser();
        var original = new HashSet<string> { "a", "b" };

        var clone = parser.Clone(original);

        Assert.NotNull(clone);
        Assert.NotSame(original, clone);
        Assert.Equal(original.Order(), clone.Order());
    }

    [Fact]
    public void StringSetParser_Clone_WhenNull_ReturnsNull()
    {
        var parser = new StringSetParser();

        Assert.Null(parser.Clone(null));
    }

    #endregion

    #region DateTimeOffsetParser Tests

    [Fact]
    public void DateTimeOffsetParser_GetStringValues_ReturnsUnixSeconds()
    {
        var parser = new DateTimeOffsetParser();
        var value = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);

        var result = parser.GetStringValues(Environment, AllowMissingDescriptor, value);

        Assert.Equal("1700000000", result);
    }

    [Fact]
    public void DateTimeOffsetParser_Parse_ReturnsDateTimeOffset()
    {
        var parser = new DateTimeOffsetParser();

        var result = parser.Parse(Environment, AllowMissingDescriptor, "1700000000");

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), result);
    }

    [Fact]
    public void DateTimeOffsetParser_Parse_WhenMissingAndAllowed_ReturnsDefault()
    {
        var parser = new DateTimeOffsetParser();

        var result = parser.Parse(Environment, AllowMissingDescriptor, StringValues.Empty);

        Assert.Equal(default, result);
    }

    #endregion
}
