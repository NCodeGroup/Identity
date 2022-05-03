using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Validation;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

public class JsonParserTests : IDisposable
{
    private readonly MockRepository _mockRepository;
    private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

    public JsonParserTests()
    {
        _mockRepository = new MockRepository(MockBehavior.Strict);
        _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
    }

    public void Dispose()
    {
        _mockRepository.Verify();
    }

    [Fact]
    public void Load_ThenValid()
    {
        var parser = new JsonParser<TestNestedObject>();

        var context = _mockOpenIdMessageContext.Object;
        var jsonSerializerOptions = new JsonSerializerOptions();
        var converter = (JsonConverter<TestNestedObject>)jsonSerializerOptions.GetConverter(typeof(TestNestedObject));

        const string parameterName = "parameterName";
        var expectedValue = new TestNestedObject { NestedPropertyName1 = "NestedPropertyValue" };
        var expectedValueAsJson = JsonSerializer.Serialize(expectedValue);

        var descriptor = new ParameterDescriptor(parameterName);

        var buffer = new ArrayBufferWriter<byte>();
        var writer = new Utf8JsonWriter(buffer);
        converter.Write(writer, expectedValue, jsonSerializerOptions);
        writer.Flush();

        var reader = new Utf8JsonReader(buffer.WrittenSpan);

        Assert.True(reader.Read());

        var parameter = parser.Load(context, descriptor, ref reader, jsonSerializerOptions);

        Assert.Equal(expectedValueAsJson, parameter.StringValues);
        Assert.Equal(expectedValueAsJson, JsonSerializer.Serialize(parameter.ParsedValue));
    }

    [Fact]
    public void Serialize_ThenValid()
    {
        var parser = new JsonParser<TestNestedObject>();

        var context = _mockOpenIdMessageContext.Object;
        var jsonSerializerOptions = new JsonSerializerOptions();

        _mockOpenIdMessageContext
            .Setup(_ => _.JsonSerializerOptions)
            .Returns(jsonSerializerOptions)
            .Verifiable();

        var expectedValue = new TestNestedObject { NestedPropertyName1 = "NestedPropertyValue" };
        var expectedValueAsJson = JsonSerializer.Serialize(expectedValue);

        var stringValues = parser.Serialize(context, expectedValue);
        Assert.Equal(expectedValueAsJson, stringValues);
    }

    [Fact]
    public void Parse_GivenEmpty_WhenOptional_ThenValid()
    {
        var parser = new JsonParser<TestNestedObject>();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<TestNestedObject?>(
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
        var parser = new JsonParser<TestNestedObject>();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<TestNestedObject?>(
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
    public void Parse_GivenMultipleValues_ThenThrows()
    {
        var parser = new JsonParser<TestNestedObject>();
        var context = _mockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var knownParameter = new KnownParameter<TestNestedObject?>(
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
    public void Parse_GivenValidJson_ThenValid()
    {
        var parser = new JsonParser<TestNestedObject>();
        var context = _mockOpenIdMessageContext.Object;
        var jsonSerializerOptions = new JsonSerializerOptions();

        _mockOpenIdMessageContext
            .Setup(_ => _.JsonSerializerOptions)
            .Returns(jsonSerializerOptions)
            .Verifiable();

        const string parameterName = "parameterName";
        var expectedValue = new TestNestedObject { NestedPropertyName1 = "NestedPropertyValue" };
        var expectedValueAsJson = JsonSerializer.Serialize(expectedValue);

        var knownParameter = new KnownParameter<TestNestedObject?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        var result = parser.Parse(context, descriptor, expectedValueAsJson);
        Assert.Equal(expectedValueAsJson, JsonSerializer.Serialize(result));
    }

    [Fact]
    public void Parse_GivenInvalidJson_ThenThrows()
    {
        var parser = new JsonParser<TestNestedObject>();
        var context = _mockOpenIdMessageContext.Object;
        var jsonSerializerOptions = new JsonSerializerOptions();

        _mockOpenIdMessageContext
            .Setup(_ => _.JsonSerializerOptions)
            .Returns(jsonSerializerOptions)
            .Verifiable();

        _mockOpenIdMessageContext
            .Setup(_ => _.Logger)
            .Returns(NullLogger.Instance)
            .Verifiable();

        const string parameterName = "parameterName";
        const string stringValues = "@invalid_json$";

        var knownParameter = new KnownParameter<TestNestedObject?>(
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
}