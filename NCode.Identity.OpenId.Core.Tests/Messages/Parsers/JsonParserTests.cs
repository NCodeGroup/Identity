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

using System.Buffers;
using System.Text.Json;
using Moq;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using NCode.Identity.OpenId.Results;
using NCode.Identity.OpenId.Servers;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages.Parsers;

// TODO: unit tests for IgnoreErrors
// TODO: unit tests for GetJsonConverter

public class JsonParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<OpenIdServer> MockOpenIdServer { get; }

    public JsonParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdServer = MockRepository.Create<OpenIdServer>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void Load_ThenValid()
    {
        var parser = new JsonParser<TestNestedObject>();

        var jsonSerializerOptions = new JsonSerializerOptions();

        const string parameterName = "parameterName";
        var expectedValue = new TestNestedObject { NestedPropertyName1 = "NestedPropertyValue" };
        var expectedValueAsJson = JsonSerializer.Serialize(expectedValue);

        var descriptor = new ParameterDescriptor(parameterName);

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Utf8JsonWriter(buffer);
        JsonSerializer.Serialize(writer, expectedValue, jsonSerializerOptions);
        writer.Flush();

        var reader = new Utf8JsonReader(buffer.WrittenSpan);

        Assert.True(reader.Read());

        var parameter = parser.Read(ref reader, MockOpenIdServer.Object, descriptor, jsonSerializerOptions);
        var typedParameter = Assert.IsType<Parameter<TestNestedObject>>(parameter);

        Assert.Equal(expectedValueAsJson, typedParameter.StringValues);
        Assert.Equal(expectedValueAsJson, JsonSerializer.Serialize(typedParameter.ParsedValue));
    }

    [Fact]
    public void Serialize_ThenValid()
    {
        var parser = new JsonParser<TestNestedObject>();

        var server = MockOpenIdServer.Object;
        var jsonSerializerOptions = new JsonSerializerOptions();

        MockOpenIdServer
            .Setup(x => x.JsonSerializerOptions)
            .Returns(jsonSerializerOptions)
            .Verifiable();

        var expectedValue = new TestNestedObject { NestedPropertyName1 = "NestedPropertyValue" };
        var expectedValueAsJson = JsonSerializer.Serialize(expectedValue);

        var stringValues = parser.Serialize(server, expectedValue);
        Assert.Equal(expectedValueAsJson, stringValues);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        var parser = new JsonParser<TestNestedObject>();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, parser)
        {
            Optional = true,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(server, descriptor, stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenRequired_ThenThrows()
    {
        var parser = new JsonParser<TestNestedObject>();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdServer
            .Setup(x => x.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
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
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenMultipleValues_ThenThrows()
    {
        var parser = new JsonParser<TestNestedObject>();
        var server = MockOpenIdServer.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdServer
            .Setup(x => x.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
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
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));
    }

    [Fact]
    public void Parse_GivenValidJson_ThenValid()
    {
        var parser = new JsonParser<TestNestedObject>();
        var server = MockOpenIdServer.Object;
        var jsonSerializerOptions = new JsonSerializerOptions();

        MockOpenIdServer
            .Setup(x => x.JsonSerializerOptions)
            .Returns(jsonSerializerOptions)
            .Verifiable();

        const string parameterName = "parameterName";
        var expectedValue = new TestNestedObject { NestedPropertyName1 = "NestedPropertyValue" };
        var expectedValueAsJson = JsonSerializer.Serialize(expectedValue);

        var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, parser)
        {
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(server, descriptor, expectedValueAsJson);
        Assert.Equal(expectedValueAsJson, JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Parse_GivenInvalidJson_ThenThrows()
    {
        var parser = new JsonParser<TestNestedObject>();
        var server = MockOpenIdServer.Object;
        var jsonSerializerOptions = new JsonSerializerOptions();

        MockOpenIdServer
            .Setup(x => x.JsonSerializerOptions)
            .Returns(jsonSerializerOptions)
            .Verifiable();

        const string parameterName = "parameterName";
        const string stringValues = "@invalid_json$";

        var mockOpenIdErrorFactory = MockRepository.Create<IOpenIdErrorFactory>();
        MockOpenIdServer
            .Setup(x => x.ErrorFactory)
            .Returns(mockOpenIdErrorFactory.Object)
            .Verifiable();

        var mockOpenIdError = MockRepository.Create<IOpenIdError>();
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
            Optional = false,
            AllowMultipleStringValues = false
        };

        var descriptor = new ParameterDescriptor(knownParameter);

        var exception = Assert.Throws<OpenIdException>(() =>
            parser.Parse(server, descriptor, stringValues));

        var innerException = mockOpenIdError.Object.Exception;
        Assert.NotNull(innerException);
        Assert.Same(innerException, exception.InnerException);
    }
}
