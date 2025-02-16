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

using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

// TODO: unit tests for IgnoreErrors

public class StringParserTests : BaseTests
{
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }

    public StringParserTests()
    {
        MockOpenIdEnvironment = CreateStrictMock<OpenIdEnvironment>();
    }

    [Fact]
    public void Format_ThenValid()
    {
        var parser = new StringParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        var descriptor = new ParameterDescriptor(parameterName, ParameterLoader.Default);

        const string parsedValue = "parsedValue";

        var stringValues = parser.GetStringValues(environment, descriptor, parsedValue);
        Assert.Equal(parsedValue, stringValues);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        var parser = new StringParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<string?>(parameterName, parser)
        {
            AllowMissingStringValues = true,
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(environment, descriptor, stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenRequired_ThenThrows()
    {
        var parser = new StringParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var mockOpenIdErrorFactory = CreateStrictMock<IOpenIdErrorFactory>();
        MockOpenIdEnvironment
            .Setup(x => x.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = CreateStrictMock<IOpenIdError>();
        mockOpenIdErrorFactory
            .Setup(x => x.Create(OpenIdConstants.ErrorCodes.InvalidRequest))
            .Returns(mockOpenIdError.Object)
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Code)
            .Returns(OpenIdConstants.ErrorCodes.InvalidRequest)
            .Verifiable();

        mockOpenIdError
            .SetupSet(x => x.Description = $"The request is missing the '{parameterName}' parameter.")
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<string?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(environment, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenMultipleValues_ThenThrows()
    {
        var parser = new StringParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var mockOpenIdErrorFactory = CreateStrictMock<IOpenIdErrorFactory>();
        MockOpenIdEnvironment
            .Setup(x => x.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = CreateStrictMock<IOpenIdError>();
        mockOpenIdErrorFactory
            .Setup(x => x.Create(OpenIdConstants.ErrorCodes.InvalidRequest))
            .Returns(mockOpenIdError.Object)
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Code)
            .Returns(OpenIdConstants.ErrorCodes.InvalidRequest)
            .Verifiable();

        mockOpenIdError
            .SetupSet(x => x.Description = $"The request includes the '{parameterName}' parameter more than once.")
            .Verifiable();

        mockOpenIdError
            .Setup(x => x.Exception)
            .Returns((Exception?)null)
            .Verifiable();

        var knownParameter = new KnownParameter<string?>(parameterName, parser)
        {
            AllowMissingStringValues = true,
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(environment, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenSingleStringValue_ThenValid()
    {
        var parser = new StringParser();
        var environment = MockOpenIdEnvironment.Object;

        const string parameterName = "parameterName";
        const string stringValues = "value1";

        var knownParameter = new KnownParameter<string?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(environment, descriptor, stringValues);
        Assert.Equal(stringValues, result);
    }
}
