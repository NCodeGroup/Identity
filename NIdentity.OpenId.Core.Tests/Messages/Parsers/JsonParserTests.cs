using System.Buffers;
using System.Text.Json;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Validation;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

public class JsonParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IOpenIdMessageContext> MockOpenIdMessageContext { get; }

    public JsonParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdMessageContext = MockRepository.Create<IOpenIdMessageContext>();
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

        var parameter = parser.Read(ref reader, descriptor, jsonSerializerOptions);
        var typedParameter = Assert.IsType<Parameter<TestNestedObject>>(parameter);

        Assert.Equal(expectedValueAsJson, typedParameter.StringValues);
        Assert.Equal(expectedValueAsJson, JsonSerializer.Serialize(typedParameter.ParsedValue));
    }

    [Fact]
    public void Serialize_ThenValid()
    {
        var parser = new JsonParser<TestNestedObject>();

        var context = MockOpenIdMessageContext.Object;
        var jsonSerializerOptions = new JsonSerializerOptions();

        MockOpenIdMessageContext
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
        var context = MockOpenIdMessageContext.Object;

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
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = Array.Empty<string>();

        var knownParameter = new KnownParameter<TestNestedObject?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() => { parser.Parse(context, descriptor, stringValues); });
    }

    [Fact]
    public void Parse_GivenMultipleValues_ThenThrows()
    {
        var parser = new JsonParser<TestNestedObject>();
        var context = MockOpenIdMessageContext.Object;

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var knownParameter = new KnownParameter<TestNestedObject?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() => { parser.Parse(context, descriptor, stringValues); });
    }

    [Fact]
    public void Parse_GivenValidJson_ThenValid()
    {
        var parser = new JsonParser<TestNestedObject>();
        var context = MockOpenIdMessageContext.Object;
        var jsonSerializerOptions = new JsonSerializerOptions();

        MockOpenIdMessageContext
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
        var context = MockOpenIdMessageContext.Object;
        var jsonSerializerOptions = new JsonSerializerOptions();

        MockOpenIdMessageContext
            .Setup(_ => _.JsonSerializerOptions)
            .Returns(jsonSerializerOptions)
            .Verifiable();

        const string parameterName = "parameterName";
        const string stringValues = "@invalid_json$";

        var knownParameter = new KnownParameter<TestNestedObject?>(
            parameterName,
            optional: false,
            allowMultipleValues: false,
            parser);

        var descriptor = new ParameterDescriptor(knownParameter);

        Assert.Throws<OpenIdException>(() => { parser.Parse(context, descriptor, stringValues); });
    }
}
