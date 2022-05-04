using System.Buffers;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Validation;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages;

public class OpenIdMessageJsonConverterTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IOpenIdMessageContext> MockOpenIdMessageContext { get; }

    public OpenIdMessageJsonConverterTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdMessageContext = MockRepository.Create<IOpenIdMessageContext>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
    }

    [Fact]
    public void LoadParameter_GivenInvalidTokenType_ThenThrows()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var jsonSerializerOptions = new JsonSerializerOptions();
        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";

        jsonWriter.WriteNullValue();
        jsonWriter.Flush();

        Assert.Throws<JsonException>(() =>
        {
            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            converter.LoadParameter(parameterName, ref reader, jsonSerializerOptions);
        });
    }

    [Fact]
    public void LoadParameter_GivenStringValueInObject_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var jsonSerializerOptions = new JsonSerializerOptions();
        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const string stringValue = "stringValue";
        var expectedValue = JsonSerializer.Serialize(new Dictionary<string, object> { [parameterName] = stringValue });

        KnownParameter? knownParameter = null;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameter))
            .Returns(false)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteStringValue(stringValue);
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var parameter = converter.LoadParameter(parameterName, ref reader, jsonSerializerOptions);
        var typedParameter = Assert.IsType<Parameter<JsonElement>>(parameter);

        Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Null(typedParameter.Descriptor.KnownParameter);
        Assert.Equal(expectedValue, typedParameter.StringValues);
        Assert.Equal(expectedValue, JsonSerializer.Serialize(typedParameter.ParsedValue));
    }

    [Fact]
    public void LoadParameter_GivenStringValueInArray_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var jsonSerializerOptions = new JsonSerializerOptions();
        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const string stringValue = "stringValue";
        var expectedValue = JsonSerializer.Serialize(new[] { stringValue });

        KnownParameter? knownParameter = null;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameter))
            .Returns(false)
            .Verifiable();

        jsonWriter.WriteStartArray();
        jsonWriter.WriteStringValue(stringValue);
        jsonWriter.WriteEndArray();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

        var parameter = converter.LoadParameter(parameterName, ref reader, jsonSerializerOptions);
        var typedParameter = Assert.IsType<Parameter<JsonElement>>(parameter);

        Assert.Equal(JsonTokenType.EndArray, reader.TokenType);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Null(parameter.Descriptor.KnownParameter);
        Assert.Equal(expectedValue, typedParameter.StringValues);
        Assert.Equal(expectedValue, JsonSerializer.Serialize(typedParameter.ParsedValue));
    }

    [Fact]
    public void LoadParameter_GivenJsonParser_WhenSuccess_ThenParameterAdded()
    {
        var context = MockOpenIdMessageContext.Object;
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(context);

        var jsonSerializerOptions = new JsonSerializerOptions();
        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const string stringValue = "stringValue";
        const bool optional = true;
        const bool allowMultipleValues = true;
        var expectedValue = JsonSerializer.Serialize(new Dictionary<string, object> { [parameterName] = stringValue });

        var mockTestParameterParser = MockRepository.Create<ITestParameterParser>();
        var parser = new TestParameterParser(mockTestParameterParser.Object, LoadJsonValid);

        var knownParameter = new KnownParameter<string>(parameterName, optional, allowMultipleValues, parser);

        KnownParameter? knownParameterBase = knownParameter;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameterBase))
            .Returns(true)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteStringValue(stringValue);
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var parameter = converter.LoadParameter(parameterName, ref reader, jsonSerializerOptions);
        var typedParameter = Assert.IsType<Parameter<JsonElement>>(parameter);

        Assert.Equal(JsonTokenType.EndObject, reader.TokenType);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Same(knownParameter, typedParameter.Descriptor.KnownParameter);
        Assert.Equal(expectedValue, typedParameter.StringValues);
        Assert.Equal(expectedValue, JsonSerializer.Serialize(typedParameter.ParsedValue));
    }

    private static Parameter LoadJsonValid(IOpenIdMessageContext context, ParameterDescriptor descriptor, ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

        var parameterName = reader.GetString();
        Assert.NotNull(parameterName);
        Assert.Equal(descriptor.ParameterName, parameterName);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.String, reader.TokenType);

        var stringValue = reader.GetString();
        Assert.NotNull(stringValue);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.EndObject, reader.TokenType);

        var json = JsonSerializer.Serialize(new Dictionary<string, object> { [parameterName!] = stringValue! });
        var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

        return new Parameter<JsonElement>(descriptor, json, jsonElement);
    }

    [Fact]
    public void LoadParameter_GivenJsonParser_WhenFailure_ThenParameterNotAdded()
    {
        var context = MockOpenIdMessageContext.Object;
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(context);

        var jsonSerializerOptions = new JsonSerializerOptions();
        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const string stringValue = "stringValue";
        const bool optional = true;
        const bool allowMultipleValues = true;

        var mockTestParameterParser = MockRepository.Create<ITestParameterParser>();
        var parser = new TestParameterParser(mockTestParameterParser.Object, LoadJsonThrows);

        var knownParameter = new KnownParameter<string>(parameterName, optional, allowMultipleValues, parser);

        KnownParameter? knownParameterBase = knownParameter;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameterBase))
            .Returns(true)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteStringValue(stringValue);
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var message = new TestOpenIdMessage();
        message.Initialize(context, Array.Empty<Parameter>());

        Assert.Throws<InvalidOperationException>(() =>
        {
            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            converter.LoadParameter(parameterName, ref reader, jsonSerializerOptions);
        });

        Assert.Empty(message.Parameters);
    }

    private static Parameter LoadJsonThrows(IOpenIdMessageContext context, ParameterDescriptor descriptor, ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        throw new InvalidOperationException();
    }

    [Fact]
    public void Read_GivenNull_ThenReturnDefault()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        jsonWriter.WriteNullValue();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.Null, reader.TokenType);

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.Null(message);
    }

    [Fact]
    public void Read_GivenNotStartObject_ThenThrows()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        jsonWriter.WriteStringValue("propertyValue");
        jsonWriter.Flush();

        Assert.Throws<JsonException>(() =>
        {
            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.NotEqual(JsonTokenType.StartObject, reader.TokenType);

            return converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        });
    }

    [Fact]
    public void Read_GivenEndObject_ThenReturnEmptyObject()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        jsonWriter.WriteStartObject();
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Same(MockOpenIdMessageContext.Object, message!.Context);
        Assert.Empty(message);
    }

    [Fact]
    public void Read_GivenNull_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";

        KnownParameter? knownParameter = null;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameter))
            .Returns(false)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteNullValue();
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Same(MockOpenIdMessageContext.Object, message!.Context);

        var (key, parameter) = Assert.Single(message.Parameters);
        var typedParameter = Assert.IsType<Parameter<StringValues>>(parameter);

        Assert.Equal(parameterName, key);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Equal(StringValues.Empty, typedParameter.StringValues);
        Assert.Empty(typedParameter.ParsedValue);
    }

    [Fact]
    public void Read_GivenString_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const string expectedValue = "value1";

        KnownParameter? knownParameter = null;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameter))
            .Returns(false)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteStringValue(expectedValue);
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Same(MockOpenIdMessageContext.Object, message!.Context);

        var (key, parameter) = Assert.Single(message.Parameters);
        var typedParameter = Assert.IsType<Parameter<StringValues>>(parameter);

        Assert.Equal(parameterName, key);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Equal(expectedValue, typedParameter.StringValues);
        Assert.Equal(expectedValue, typedParameter.ParsedValue);
    }

    [Fact]
    public void Read_GivenStringList_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        var expectedValue = new[] { "value1", "value2" };

        KnownParameter? knownParameter = null;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameter))
            .Returns(false)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteStringValue(string.Join(OpenIdConstants.ParameterSeparator, expectedValue));
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Same(MockOpenIdMessageContext.Object, message!.Context);

        var (key, parameter) = Assert.Single(message.Parameters);
        var typedParameter = Assert.IsType<Parameter<StringValues>>(parameter);

        Assert.Equal(parameterName, key);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Equal(expectedValue, typedParameter.StringValues);
        Assert.Equal(expectedValue, typedParameter.ParsedValue);
    }

    [Fact]
    public void Read_GivenNumberAsDecimal_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const decimal expectedValue = 3.1415m;
        var expectedValueAsString = expectedValue.ToString(CultureInfo.InvariantCulture);

        KnownParameter? knownParameter = null;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameter))
            .Returns(false)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteNumberValue(expectedValue);
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Same(MockOpenIdMessageContext.Object, message!.Context);

        var (key, parameter) = Assert.Single(message.Parameters);
        var typedParameter = Assert.IsType<Parameter<StringValues>>(parameter);

        Assert.Equal(parameterName, key);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Equal(expectedValueAsString, typedParameter.StringValues);
        Assert.Equal(expectedValueAsString, typedParameter.ParsedValue);
    }

    [Fact]
    public void Read_GivenNumberAsInt32_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const int expectedValue = 3;
        var expectedValueAsString = expectedValue.ToString(CultureInfo.InvariantCulture);

        KnownParameter? knownParameter = null;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameter))
            .Returns(false)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteNumberValue(expectedValue);
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Same(MockOpenIdMessageContext.Object, message!.Context);

        var (key, parameter) = Assert.Single(message.Parameters);
        var typedParameter = Assert.IsType<Parameter<StringValues>>(parameter);

        Assert.Equal(parameterName, key);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Equal(expectedValueAsString, typedParameter.StringValues);
        Assert.Equal(expectedValueAsString, typedParameter.ParsedValue);
    }

    [Fact]
    public void Read_GivenBooleanAsTrue_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const bool expectedValue = true;
        var expectedValueAsString = expectedValue.ToString(CultureInfo.InvariantCulture);

        KnownParameter? knownParameter = null;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameter))
            .Returns(false)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteBooleanValue(expectedValue);
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Same(MockOpenIdMessageContext.Object, message!.Context);

        var (key, parameter) = Assert.Single(message.Parameters);
        var typedParameter = Assert.IsType<Parameter<StringValues>>(parameter);

        Assert.Equal(parameterName, key);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Equal(expectedValueAsString, typedParameter.StringValues);
        Assert.Equal(bool.TrueString, typedParameter.ParsedValue);
    }

    [Fact]
    public void Read_GivenBooleanAsFalse_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const bool expectedValue = false;
        var expectedValueAsString = expectedValue.ToString(CultureInfo.InvariantCulture);

        KnownParameter? knownParameter = null;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameter))
            .Returns(false)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteBooleanValue(expectedValue);
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Same(MockOpenIdMessageContext.Object, message!.Context);

        var (key, parameter) = Assert.Single(message.Parameters);
        var typedParameter = Assert.IsType<Parameter<StringValues>>(parameter);

        Assert.Equal(parameterName, key);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Equal(expectedValueAsString, typedParameter.StringValues);
        Assert.Equal(bool.FalseString, typedParameter.ParsedValue);
    }

    [Fact]
    public void Read_GivenNestedArray_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const string stringValue = "value1";
        var expectedValue = JsonSerializer.Serialize(new[] { stringValue });

        KnownParameter? knownParameter = null;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameter))
            .Returns(false)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteStartArray();
        jsonWriter.WriteStringValue(stringValue);
        jsonWriter.WriteEndArray();
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Same(MockOpenIdMessageContext.Object, message!.Context);

        var (key, parameter) = Assert.Single(message.Parameters);
        var typedParameter = Assert.IsType<Parameter<JsonElement>>(parameter);

        Assert.Equal(parameterName, key);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Equal(expectedValue, typedParameter.StringValues);
        Assert.Equal(expectedValue, JsonSerializer.Serialize(typedParameter.ParsedValue));
    }

    [Fact]
    public void Read_GivenNestedObject_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const string nestedStringValue = "value1";
        var expectedValue = JsonSerializer.Serialize(new TestNestedObject { NestedPropertyName1 = nestedStringValue });

        KnownParameter? knownParameter = null;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameter))
            .Returns(false)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(nameof(TestNestedObject.NestedPropertyName1));
        jsonWriter.WriteStringValue(nestedStringValue);
        jsonWriter.WriteEndObject();
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Same(MockOpenIdMessageContext.Object, message!.Context);

        var (key, parameter) = Assert.Single(message.Parameters);
        var typedParameter = Assert.IsType<Parameter<JsonElement>>(parameter);

        Assert.Equal(parameterName, key);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Equal(expectedValue, typedParameter.StringValues);
        Assert.Equal(expectedValue, JsonSerializer.Serialize(typedParameter.ParsedValue));
    }

    [Fact]
    public void Read_GivenNestedObject_WithJsonConverter_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions
        {
            Converters = { new TestNestedObjectJsonConverter() }
        };

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const string nestedStringValue = "value1";
        const bool optional = false;
        const bool allowMultipleValues = false;

        var knownParameter = new KnownParameter<string?>(
            parameterName,
            optional,
            allowMultipleValues,
            ParameterParsers.String);

        KnownParameter? knownParameterBase = knownParameter;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameterBase))
            .Returns(true)
            .Verifiable();

        var expectedValue = JsonSerializer.Serialize(new TestNestedObject { NestedPropertyName1 = nestedStringValue });

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName("NestedPropertyName1");
        jsonWriter.WriteStringValue(nestedStringValue);
        jsonWriter.WriteEndObject();
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.NotNull(message);
        Assert.Same(MockOpenIdMessageContext.Object, message!.Context);

        var (key, parameter) = Assert.Single(message.Parameters);
        var typedParameter = Assert.IsType<Parameter<JsonElement>>(parameter);

        Assert.Equal(parameterName, key);
        Assert.Equal(parameterName, typedParameter.Descriptor.ParameterName);
        Assert.Equal(expectedValue, typedParameter.StringValues);
        Assert.Equal(expectedValue, JsonSerializer.Serialize(typedParameter.ParsedValue));
    }

    [Fact]
    public void Read_GivenLoadFailure_ThenThrows()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var typeToConvert = typeof(TestOpenIdMessage);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        const string parameterName = "parameterName";
        const bool optional = false;
        const bool allowMultipleValues = false;

        var knownParameter = new KnownParameter<string?>(
            parameterName,
            optional,
            allowMultipleValues,
            ParameterParsers.String);

        KnownParameter? knownParameterBase = knownParameter;
        MockOpenIdMessageContext
            .Setup(_ => _.TryGetKnownParameter(parameterName, out knownParameterBase))
            .Returns(true)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteNullValue();
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        Assert.Throws<OpenIdException>(() =>
        {
            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        });
    }

    [Fact]
    public void Write_GivenNull_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(MockOpenIdMessageContext.Object);

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);
        var jsonSerializerOptions = new JsonSerializerOptions();

        const TestOpenIdMessage? message = null;

        converter.Write(jsonWriter, message, jsonSerializerOptions);
        jsonWriter.Flush();

        var jsonReader = new Utf8JsonReader(bufferWriter.WrittenSpan);
        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.Null, jsonReader.TokenType);
    }

    [Fact]
    public void Write_GivenEmpty_ThenValid()
    {
        var context = MockOpenIdMessageContext.Object;
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(context);

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);
        var jsonSerializerOptions = new JsonSerializerOptions();

        var message = new TestOpenIdMessage();
        message.Initialize(context, Array.Empty<Parameter>());

        converter.Write(jsonWriter, message, jsonSerializerOptions);
        jsonWriter.Flush();

        var jsonReader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.StartObject, jsonReader.TokenType);

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);
    }

    [Fact]
    public void Write_GivenStringValue_ThenValid()
    {
        var context = MockOpenIdMessageContext.Object;
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(context);

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);
        var jsonSerializerOptions = new JsonSerializerOptions();

        const string parameterName = "parameterName";
        const string stringValue = "stringValue";

        var message = new TestOpenIdMessage();
        var parameter = new Parameter<string>(new ParameterDescriptor(parameterName), stringValue);
        message.Initialize(context, new[] { parameter });

        converter.Write(jsonWriter, message, jsonSerializerOptions);
        jsonWriter.Flush();

        var jsonReader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.StartObject, jsonReader.TokenType);

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal(parameterName, jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(stringValue, jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);
    }

    [Fact]
    public void Write_GivenStringList_ThenValid()
    {
        var context = MockOpenIdMessageContext.Object;
        var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(context);

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);
        var jsonSerializerOptions = new JsonSerializerOptions();

        const string parameterName = "parameterName";
        var stringValues = new[] { "value1", "value2" };

        var message = new TestOpenIdMessage();
        var parameter = new Parameter<string[]>(new ParameterDescriptor(parameterName), stringValues);
        message.Initialize(context, new[] { parameter });

        converter.Write(jsonWriter, message, jsonSerializerOptions);
        jsonWriter.Flush();

        var jsonReader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.StartObject, jsonReader.TokenType);

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal(parameterName, jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(string.Join(OpenIdConstants.ParameterSeparator, stringValues), jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);
    }
}
