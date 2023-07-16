#region Copyright Preamble
// 
//    Copyright @ 2023 NCode Group
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
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Results;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

// TODO: unit tests for IgnoreErrors

public class StringSetParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IOpenIdContext> MockOpenIdContext { get; }

    public StringSetParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdContext = MockRepository.Create<IOpenIdContext>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Serialize_GivenNull_ThenEmpty()
    {
        var parser = new StringSetParser();
        var context = MockOpenIdContext.Object;

        var parsedValue = (IReadOnlyCollection<string>?)null;

        var stringValues = parser.Serialize(context, parsedValue);
        Assert.Equal(StringValues.Empty, stringValues);
    }

    [Fact]
    public void Serialize_GivenEmpty_ThenEmpty()
    {
        var parser = new StringSetParser();
        var context = MockOpenIdContext.Object;

        var parsedValue = Array.Empty<string>();

        var stringValues = parser.Serialize(context, parsedValue);
        Assert.Equal(StringValues.Empty, stringValues);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        var parser = new StringSetParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
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
        var parser = new StringSetParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdContext
            .Setup(_ => _.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
        mockOpenIdErrorFactory
            .Setup(_ => _.Create(OpenIdConstants.ErrorCodes.InvalidRequest))
            .Returns(mockOpenIdError.Object)
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Code)
            .Returns(OpenIdConstants.ErrorCodes.InvalidRequest)
            .Verifiable();

        mockOpenIdError
            .SetupSet(_ => _.Description = $"The request is missing the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenMultipleValues_WhenDisallowMultipleValues_ThenThrows()
    {
        var parser = new StringSetParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdContext
            .Setup(_ => _.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
        mockOpenIdErrorFactory
            .Setup(_ => _.Create(OpenIdConstants.ErrorCodes.InvalidRequest))
            .Returns(mockOpenIdError.Object)
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Code)
            .Returns(OpenIdConstants.ErrorCodes.InvalidRequest)
            .Verifiable();

        mockOpenIdError
            .SetupSet(_ => _.Description = $"The request includes the '{parameterName}' parameter more than once.")
            .Verifiable();

        mockOpenIdError
            .Setup(_ => _.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(context, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenMultipleValues_WhenAllowMultipleValues_ThenValid()
    {
        var parser = new StringSetParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
            parameterName,
            optional: false,
            allowMultipleValues: true,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(stringValues, result!);
    }

    [Fact]
    public void Parse_GivenDuplicateValues_WhenAllowMultipleValues_ThenDistinctValues()
    {
        var parser = new StringSetParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2", "value1", "value2" };

        var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
            parameterName,
            optional: false,
            allowMultipleValues: true,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(stringValues.Distinct(), result);
    }

    [Fact]
    public void Parse_GivenStringList_ThenValid()
    {
        var parser = new StringSetParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "value1 value2";
        var expectedResult = new[] { "value1", "value2" };

        var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(expectedResult, result!);
    }

    [Fact]
    public void Parse_GivenStringListWithDuplicates_ThenDistinctValues()
    {
        var parser = new StringSetParser();
        var context = MockOpenIdContext.Object;

        const string parameterName = "parameterName";
        const string stringValues = "value1 value2 value1 value2";
        var expectedResult = new[] { "value1", "value2" };

        var knownParameter = new KnownParameter<IReadOnlyCollection<string>?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, stringValues);
        Assert.Equal(expectedResult, result!);
    }
}
