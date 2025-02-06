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

using System.Text.Json;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

// TODO: unit tests for IgnoreErrors
// TODO: unit tests for GetJsonConverter

public class JsonParserTests : BaseTests
{
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }

    public JsonParserTests()
    {
        MockOpenIdEnvironment = CreateStrictMock<OpenIdEnvironment>();
    }

    [Fact]
    public void Serialize_ThenValid()
    {
        const string parameterName = nameof(parameterName);

        var environment = MockOpenIdEnvironment.Object;
        var parser = new JsonParser<TestNestedObject>();
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        var descriptor = new ParameterDescriptor(parameterName, ParameterLoader.Default);

        MockOpenIdEnvironment
            .Setup(x => x.JsonSerializerOptions)
            .Returns(jsonSerializerOptions)
            .Verifiable();

        var expectedValue = new TestNestedObject { NestedPropertyName1 = "NestedPropertyValue" };
        var expectedValueAsJson = JsonSerializer.Serialize(expectedValue, jsonSerializerOptions);

        var stringValues = parser.Serialize(environment, descriptor, expectedValue);
        Assert.Equal(expectedValueAsJson, stringValues);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        const string parameterName = nameof(parameterName);

        var environment = MockOpenIdEnvironment.Object;
        var parser = new JsonParser<TestNestedObject>();
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, parser)
        {
            AllowMissingStringValues = true,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(environment, descriptor, stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenRequired_ThenThrows()
    {
        const string parameterName = nameof(parameterName);

        var environment = MockOpenIdEnvironment.Object;
        var parser = new JsonParser<TestNestedObject>();
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

        var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(environment, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenMultipleValues_ThenThrows()
    {
        const string parameterName = nameof(parameterName);
        var stringValues = new[] { "value1", "value2" };

        var environment = MockOpenIdEnvironment.Object;
        var parser = new JsonParser<TestNestedObject>();

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

        var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(environment, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenValidJson_ThenValid()
    {
        const string parameterName = nameof(parameterName);

        var environment = MockOpenIdEnvironment.Object;
        var parser = new JsonParser<TestNestedObject>();
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        MockOpenIdEnvironment
            .Setup(x => x.JsonSerializerOptions)
            .Returns(jsonSerializerOptions)
            .Verifiable();

        var expectedValue = new TestNestedObject { NestedPropertyName1 = "NestedPropertyValue" };
        var expectedValueAsJson = JsonSerializer.Serialize(expectedValue);

        var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(environment, descriptor, expectedValueAsJson);
        Assert.Equal(expectedValueAsJson, JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Parse_GivenInvalidJson_ThenThrows()
    {
        const string parameterName = nameof(parameterName);
        const string stringValues = "@invalid_json$";

        var environment = MockOpenIdEnvironment.Object;
        var parser = new JsonParser<TestNestedObject>();
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        MockOpenIdEnvironment
            .Setup(x => x.JsonSerializerOptions)
            .Returns(jsonSerializerOptions)
            .Verifiable();

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
            .SetupSet(x => x.Description = "An error occurred while attempting to deserialize the JSON value.")
            .Verifiable();

        mockOpenIdError
            .SetupProperty(x => x.Exception);

        var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, parser)
        {
            AllowMissingStringValues = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var exception = Assert.Throws<OpenIdException>(() =>
            parser.Parse(environment, descriptor, stringValues));

        var innerException = mockOpenIdError.Object.Exception;
        Assert.NotNull(innerException);
        Assert.Same(innerException, exception.InnerException);
    }
}
