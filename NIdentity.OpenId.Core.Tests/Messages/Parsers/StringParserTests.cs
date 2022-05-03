#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Validation;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

public class StringParserTests : IDisposable
{
    private readonly MockRepository _mockRepository;
    private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

    public StringParserTests()
    {
        _mockRepository = new MockRepository(MockBehavior.Strict);
        _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
    }

    public void Dispose()
    {
        _mockRepository.Verify();
    }

    [Fact]
    public void Serialize_ThenValid()
    {
        var parser = new StringParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parsedValue = "parsedValue";

        var stringValue = parser.Serialize(context, parsedValue);
        Assert.Equal(parsedValue, stringValue);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        var parser = new StringParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<string?>(
            parameterName,
            optional: true,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenRequired_ThenThrows()
    {
        var parser = new StringParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<string?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
        {
            parser.Parse(context, descriptor, stringValues);
        });
    }

    [Fact]
    public void Parse_GivenMultipleValues_WhenAllowMultipleValues_ThenValid()
    {
        var parser = new StringParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };
        var expectedResult = string.Join(" ", stringValues);

        var knownParameter = new KnownParameter<string?>(
            parameterName,
            optional: true,
            allowMultipleValues: true,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void Parse_GivenMultipleValues_WhenDisallowMultipleValues_ThenThrows()
    {
        var parser = new StringParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var knownParameter = new KnownParameter<string?>(
            parameterName,
            optional: true,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
        {
            parser.Parse(context, descriptor, stringValues);
        });
    }

    [Fact]
    public void Parse_GivenSingleStringValue_ThenValid()
    {
        var parser = new StringParser();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "value1";

        var knownParameter = new KnownParameter<string?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(stringValues, result);
    }
}