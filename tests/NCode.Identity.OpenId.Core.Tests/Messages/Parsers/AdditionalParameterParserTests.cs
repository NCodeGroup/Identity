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
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;
using OpenIdUriParser = NCode.Identity.OpenId.Messages.Parsers.UriParser;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parsers;

public class AdditionalParameterParserTests : BaseTests
{
    private readonly OpenIdEnvironment _environment;
    private readonly ParameterDescriptor _allowMissingDescriptor = new(
        "test",
        ParameterLoader.Default
    );

    public AdditionalParameterParserTests()
    {
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockErrorFactory.Setup(x => x.Create(It.IsAny<string>())).Returns(mockError.Object);
        mockEnvironment.Setup(x => x.ErrorFactory).Returns(mockErrorFactory.Object);
        _environment = mockEnvironment.Object;
    }

    private ParameterDescriptor RequiredDescriptor<T>(IParameterParser<T> parser) =>
        new(new KnownParameter<T>("test", parser) { AllowMissingStringValues = false });

    #region StringParser Tests

    [Fact]
    public void StringParser_GetStringValues_ReturnsValue()
    {
        var parser = new StringParser();

        Assert.Equal(
            "value",
            parser.GetStringValues(_environment, _allowMissingDescriptor, "value")
        );
    }

    [Fact]
    public void StringParser_Parse_ReturnsSingleValue()
    {
        var parser = new StringParser();

        Assert.Equal("value", parser.Parse(_environment, _allowMissingDescriptor, "value"));
    }

    [Fact]
    public void StringParser_Parse_WhenMissingAndAllowed_ReturnsNull()
    {
        var parser = new StringParser();

        Assert.Null(parser.Parse(_environment, _allowMissingDescriptor, StringValues.Empty));
    }

    [Fact]
    public void StringParser_Parse_WhenMissingAndRequired_Throws()
    {
        var parser = new StringParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, RequiredDescriptor(parser), StringValues.Empty)
        );
    }

    [Fact]
    public void StringParser_Parse_WhenTooManyValues_Throws()
    {
        var parser = new StringParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, new StringValues(["a", "b"]))
        );
    }

    #endregion

    #region StringListParser Tests

    [Fact]
    public void StringListParser_GetStringValues_WhenNull_ReturnsEmpty()
    {
        var parser = new StringListParser();

        Assert.Equal(
            StringValues.Empty,
            parser.GetStringValues(_environment, _allowMissingDescriptor, null)
        );
    }

    [Fact]
    public void StringListParser_Parse_SplitsOnSeparator()
    {
        var parser = new StringListParser();
        var input =
            $"a{OpenIdConstants.ParameterSeparatorChar}b{OpenIdConstants.ParameterSeparatorChar}c";

        var result = parser.Parse(_environment, _allowMissingDescriptor, input);

        Assert.Equal(["a", "b", "c"], result);
    }

    [Fact]
    public void StringListParser_Parse_WhenMissingAndRequired_Throws()
    {
        var parser = new StringListParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, RequiredDescriptor(parser), StringValues.Empty)
        );
    }

    #endregion

    #region UriParser Tests

    [Fact]
    public void UriParser_GetStringValues_WhenNull_ReturnsEmpty()
    {
        var parser = new OpenIdUriParser();

        Assert.Equal(
            StringValues.Empty,
            parser.GetStringValues(_environment, _allowMissingDescriptor, null)
        );
    }

    [Fact]
    public void UriParser_GetStringValues_ReturnsAbsoluteUri()
    {
        var parser = new OpenIdUriParser();
        var uri = new Uri("https://example.com/path");

        Assert.Equal(
            uri.AbsoluteUri,
            parser.GetStringValues(_environment, _allowMissingDescriptor, uri)
        );
    }

    [Fact]
    public void UriParser_Parse_ReturnsUri()
    {
        var parser = new OpenIdUriParser();

        var result = parser.Parse(
            _environment,
            _allowMissingDescriptor,
            "https://example.com/path"
        );

        Assert.Equal(new Uri("https://example.com/path"), result);
    }

    [Fact]
    public void UriParser_Parse_WhenMissingAndAllowed_ReturnsNull()
    {
        var parser = new OpenIdUriParser();

        Assert.Null(parser.Parse(_environment, _allowMissingDescriptor, StringValues.Empty));
    }

    [Fact]
    public void UriParser_Parse_WhenInvalid_Throws()
    {
        var parser = new OpenIdUriParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, "not a uri")
        );
    }

    [Fact]
    public void UriParser_Parse_WhenTooManyValues_Throws()
    {
        var parser = new OpenIdUriParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(
                _environment,
                _allowMissingDescriptor,
                new StringValues(["https://a.example", "https://b.example"])
            )
        );
    }

    #endregion

    #region TimeSpanParser Tests

    [Fact]
    public void TimeSpanParser_GetStringValues_ReturnsWholeSeconds()
    {
        var parser = new TimeSpanParser();

        Assert.Equal(
            "90",
            parser.GetStringValues(_environment, _allowMissingDescriptor, TimeSpan.FromSeconds(90))
        );
    }

    [Fact]
    public void TimeSpanParser_Parse_ReturnsTimeSpan()
    {
        var parser = new TimeSpanParser();

        var result = parser.Parse(_environment, _allowMissingDescriptor, "90");

        Assert.Equal(TimeSpan.FromSeconds(90), result);
    }

    [Fact]
    public void TimeSpanParser_Parse_WhenMissingAndAllowed_ReturnsZero()
    {
        var parser = new TimeSpanParser();

        Assert.Equal(
            TimeSpan.Zero,
            parser.Parse(_environment, _allowMissingDescriptor, StringValues.Empty)
        );
    }

    [Fact]
    public void TimeSpanParser_Parse_WhenInvalid_Throws()
    {
        var parser = new TimeSpanParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, "not-a-number")
        );
    }

    [Fact]
    public void TimeSpanParser_Parse_WhenTooManyValues_Throws()
    {
        var parser = new TimeSpanParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, new StringValues(["1", "2"]))
        );
    }

    #endregion
}
