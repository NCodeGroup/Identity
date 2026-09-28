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

using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parsers;

public class ParameterParserHappyPathTests : BaseTests
{
    private readonly OpenIdEnvironment _environment;
    private readonly ParameterDescriptor _allowMissingDescriptor = new(
        "test",
        ParameterLoader.Default
    );

    public ParameterParserHappyPathTests()
    {
        // The happy paths under test never touch the environment; a strict mock guards that.
        _environment = CreateStrictMock<OpenIdEnvironment>().Object;
    }

    #region BoolParser Tests

    [Theory]
    [InlineData(true, "true")]
    [InlineData(false, "false")]
    public void BoolParser_GetStringValues_ReturnsExpected(bool value, string expected)
    {
        var parser = new BoolParser();

        var result = parser.GetStringValues(_environment, _allowMissingDescriptor, value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("TRUE", true)]
    public void BoolParser_Parse_ReturnsExpected(string input, bool expected)
    {
        var parser = new BoolParser();

        var result = parser.Parse(_environment, _allowMissingDescriptor, input);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void BoolParser_Parse_WhenMissingAndAllowed_ReturnsFalse()
    {
        var parser = new BoolParser();

        var result = parser.Parse(_environment, _allowMissingDescriptor, StringValues.Empty);

        Assert.False(result);
    }

    #endregion

    #region StringValuesParser Tests

    [Fact]
    public void StringValuesParser_GetStringValues_ReturnsSameValue()
    {
        var parser = new StringValuesParser();
        var value = new StringValues(["a", "b"]);

        var result = parser.GetStringValues(_environment, _allowMissingDescriptor, value);

        Assert.Equal(value, result);
    }

    [Fact]
    public void StringValuesParser_Parse_ReturnsSameValue()
    {
        var parser = new StringValuesParser();
        var value = new StringValues(["a", "b"]);

        var result = parser.Parse(_environment, _allowMissingDescriptor, value);

        Assert.Equal(value, result);
    }

    [Fact]
    public void StringValuesParser_Parse_WhenMissingAndAllowed_ReturnsEmpty()
    {
        var parser = new StringValuesParser();

        var result = parser.Parse(_environment, _allowMissingDescriptor, StringValues.Empty);

        Assert.Equal(StringValues.Empty, result);
    }

    #endregion

    #region StringSetParser Tests

    [Fact]
    public void StringSetParser_GetStringValues_WhenNull_ReturnsEmpty()
    {
        var parser = new StringSetParser();

        var result = parser.GetStringValues(_environment, _allowMissingDescriptor, null);

        Assert.Equal(StringValues.Empty, result);
    }

    [Fact]
    public void StringSetParser_GetStringValues_WhenEmpty_ReturnsEmpty()
    {
        var parser = new StringSetParser();

        var result = parser.GetStringValues(_environment, _allowMissingDescriptor, []);

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

        var result = parser.GetStringValues(_environment, descriptor, ["c", "a", "b"]);

        Assert.Equal(["a", "b", "c"], result);
    }

    [Fact]
    public void StringSetParser_Parse_SplitsOnSeparator()
    {
        var parser = new StringSetParser();
        var input =
            $"a{OpenIdConstants.ParameterSeparatorChar}b{OpenIdConstants.ParameterSeparatorChar}c";

        var result = parser.Parse(_environment, _allowMissingDescriptor, input);

        Assert.NotNull(result);
        Assert.Equal(["a", "b", "c"], result.Order());
    }

    [Fact]
    public void StringSetParser_Parse_WhenMissingAndAllowed_ReturnsNull()
    {
        var parser = new StringSetParser();

        var result = parser.Parse(_environment, _allowMissingDescriptor, StringValues.Empty);

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

        var result = parser.GetStringValues(_environment, _allowMissingDescriptor, value);

        Assert.Equal("1700000000", result);
    }

    [Fact]
    public void DateTimeOffsetParser_Parse_ReturnsDateTimeOffset()
    {
        var parser = new DateTimeOffsetParser();

        var result = parser.Parse(_environment, _allowMissingDescriptor, "1700000000");

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000), result);
    }

    [Fact]
    public void DateTimeOffsetParser_Parse_WhenMissingAndAllowed_ReturnsDefault()
    {
        var parser = new DateTimeOffsetParser();

        var result = parser.Parse(_environment, _allowMissingDescriptor, StringValues.Empty);

        Assert.Equal(default, result);
    }

    #endregion
}
