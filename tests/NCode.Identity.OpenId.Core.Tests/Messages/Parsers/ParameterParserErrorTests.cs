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

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

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
