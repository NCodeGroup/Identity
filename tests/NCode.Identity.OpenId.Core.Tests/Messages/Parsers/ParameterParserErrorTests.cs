// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Messages.Parsers;

public class ParameterParserErrorTests : BaseTests
{
    private readonly OpenIdEnvironment _environment;
    private readonly ParameterDescriptor _allowMissingDescriptor = new(
        "test",
        ParameterLoader.Default
    );

    public ParameterParserErrorTests()
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

    #region BoolParser Error Tests

    [Fact]
    public void BoolParser_Parse_WhenMissingAndRequired_Throws()
    {
        var parser = new BoolParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, RequiredDescriptor(parser), StringValues.Empty)
        );
    }

    [Fact]
    public void BoolParser_Parse_WhenTooManyValues_Throws()
    {
        var parser = new BoolParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, new StringValues(["true", "false"]))
        );
    }

    [Fact]
    public void BoolParser_Parse_WhenInvalidValue_Throws()
    {
        var parser = new BoolParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, "not-a-bool")
        );
    }

    #endregion

    #region DateTimeOffsetParser Error Tests

    [Fact]
    public void DateTimeOffsetParser_Parse_WhenMissingAndRequired_Throws()
    {
        var parser = new DateTimeOffsetParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, RequiredDescriptor(parser), StringValues.Empty)
        );
    }

    [Fact]
    public void DateTimeOffsetParser_Parse_WhenTooManyValues_Throws()
    {
        var parser = new DateTimeOffsetParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, new StringValues(["1", "2"]))
        );
    }

    [Fact]
    public void DateTimeOffsetParser_Parse_WhenInvalidValue_Throws()
    {
        var parser = new DateTimeOffsetParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, "not-a-number")
        );
    }

    [Theory]
    [InlineData("99999999999999")]
    [InlineData("-99999999999999")]
    public void DateTimeOffsetParser_Parse_WhenOutOfRange_Throws(string value)
    {
        var parser = new DateTimeOffsetParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, value)
        );
    }

    [Fact]
    public void DateTimeOffsetParser_Parse_WhenNullValue_Throws()
    {
        var parser = new DateTimeOffsetParser();
        var values = new StringValues([null]);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, _allowMissingDescriptor, values)
        );
    }

    #endregion

    #region StringValuesParser Error Tests

    [Fact]
    public void StringValuesParser_Parse_WhenMissingAndRequired_Throws()
    {
        var parser = new StringValuesParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, RequiredDescriptor(parser), StringValues.Empty)
        );
    }

    #endregion

    #region StringSetParser Error Tests

    [Fact]
    public void StringSetParser_Parse_WhenMissingAndRequired_Throws()
    {
        var parser = new StringSetParser();

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(_environment, RequiredDescriptor(parser), StringValues.Empty)
        );
    }

    #endregion
}
