using System;
using System.Buffers;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using NIdentity.OpenId.Validation;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages
{
    public class OpenIdMessageJsonConverterTests : IDisposable
    {
        private readonly MockRepository _mockRepository;
        private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

        public OpenIdMessageJsonConverterTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
            _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void LoadNestedJson_GivenInvalidTokenType_ThenThrows()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var jsonSerializerOptions = new JsonSerializerOptions();
            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            const string propertyName = "propertyName";

            jsonWriter.WriteNullValue();
            jsonWriter.Flush();

            var message = new TestOpenIdMessage();

            Assert.Throws<JsonException>(() =>
            {
                var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

                converter.LoadNestedJson(ref reader, jsonSerializerOptions, message, propertyName);
            });
        }

        [Fact]
        public void LoadNestedJson_GivenStringValueInObject_ThenValid()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var jsonSerializerOptions = new JsonSerializerOptions();
            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            const string propertyName = "propertyName";
            const string stringValue = "stringValue";
            var expectedValue =
                JsonSerializer.Serialize(new Dictionary<string, object> { [propertyName] = stringValue });

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteStringValue(stringValue);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var message = new TestOpenIdMessage();

            converter.LoadNestedJson(ref reader, jsonSerializerOptions, message, propertyName);

            Assert.Equal(JsonTokenType.EndObject, reader.TokenType);

            var (key, value) = Assert.Single(message.Parameters);
            Assert.Equal(propertyName, key);
            Assert.Equal(propertyName, value.Descriptor.ParameterName);
            Assert.Null(value.Descriptor.KnownParameter);
            Assert.Equal(expectedValue, value.StringValues);
            Assert.Equal(expectedValue, JsonSerializer.Serialize(value.ParsedValue));
        }

        [Fact]
        public void LoadNestedJson_GivenStringValueInArray_ThenValid()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var jsonSerializerOptions = new JsonSerializerOptions();
            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            const string propertyName = "propertyName";
            const string stringValue = "stringValue";
            var expectedValue = JsonSerializer.Serialize(new[] { stringValue });

            jsonWriter.WriteStartArray();
            jsonWriter.WriteStringValue(stringValue);
            jsonWriter.WriteEndArray();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartArray, reader.TokenType);

            var message = new TestOpenIdMessage();

            converter.LoadNestedJson(ref reader, jsonSerializerOptions, message, propertyName);

            Assert.Equal(JsonTokenType.EndArray, reader.TokenType);

            var (key, value) = Assert.Single(message.Parameters);
            Assert.Equal(propertyName, key);
            Assert.Equal(propertyName, value.Descriptor.ParameterName);
            Assert.Null(value.Descriptor.KnownParameter);
            Assert.Equal(expectedValue, value.StringValues);
            Assert.Equal(expectedValue, JsonSerializer.Serialize(value.ParsedValue));
        }

        [Fact]
        public void LoadNestedJson_GivenJsonParser_WhenSuccess_ThenParameterAdded()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var jsonSerializerOptions = new JsonSerializerOptions();
            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            // known parameters must have unique names
            var parameterName = Guid.NewGuid().ToString("N");

            const bool optional = true;
            const bool allowMultipleValues = true;
            const string stringValue = "stringValue";
            var expectedValue =
                JsonSerializer.Serialize(new Dictionary<string, object> { [parameterName] = stringValue });

            var mockTestParameterParser = _mockRepository.Create<ITestParameterParser>();
            var parser = new TestParameterParser(mockTestParameterParser.Object, LoadJsonValid);

            var knownParameter = new KnownParameter<string>(parameterName, optional, allowMultipleValues, parser);
            KnownParameters.Register(knownParameter);

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(parameterName);
            jsonWriter.WriteStringValue(stringValue);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var message = new TestOpenIdMessage();

            converter.LoadNestedJson(ref reader, jsonSerializerOptions, message, parameterName);

            Assert.Equal(JsonTokenType.EndObject, reader.TokenType);

            var (key, value) = Assert.Single(message.Parameters);
            Assert.Equal(parameterName, key);
            Assert.Equal(parameterName, value.Descriptor.ParameterName);
            Assert.Same(knownParameter, value.Descriptor.KnownParameter);
            Assert.Equal(expectedValue, value.StringValues);
            Assert.Equal(expectedValue, JsonSerializer.Serialize(value.ParsedValue));
        }

        private static void LoadJsonValid(
            IOpenIdMessageContext context,
            Parameter parameter,
            ref Utf8JsonReader reader,
            JsonSerializerOptions options)
        {
            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.PropertyName, reader.TokenType);

            var propertyName = reader.GetString();
            Assert.NotNull(propertyName);
            Assert.Equal(parameter.Descriptor.ParameterName, propertyName);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.String, reader.TokenType);

            var stringValue = reader.GetString();
            Assert.NotNull(stringValue);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.EndObject, reader.TokenType);

            var json = JsonSerializer.Serialize(new Dictionary<string, object> { [propertyName!] = stringValue! });
            var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);

            parameter.Update(json, jsonElement);
        }

        [Fact]
        public void LoadNestedJson_GivenJsonParser_WhenFailure_ThenParameterNotAdded()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var jsonSerializerOptions = new JsonSerializerOptions();
            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            // known parameters must have unique names
            var parameterName = Guid.NewGuid().ToString("N");

            const bool optional = true;
            const bool allowMultipleValues = true;
            const string stringValue = "stringValue";

            var mockTestParameterParser = _mockRepository.Create<ITestParameterParser>();
            var parser = new TestParameterParser(mockTestParameterParser.Object, LoadJsonThrows);

            var knownParameter = new KnownParameter<string>(parameterName, optional, allowMultipleValues, parser);
            KnownParameters.Register(knownParameter);

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(parameterName);
            jsonWriter.WriteStringValue(stringValue);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var message = new TestOpenIdMessage();

            Assert.Throws<InvalidOperationException>(() =>
            {
                var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

                Assert.True(reader.Read());
                Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

                converter.LoadNestedJson(ref reader, jsonSerializerOptions, message, parameterName);
            });

            Assert.Empty(message.Parameters);
        }

        private static void LoadJsonThrows(
            IOpenIdMessageContext context,
            Parameter parameter,
            ref Utf8JsonReader reader,
            JsonSerializerOptions options)
        {
            throw new InvalidOperationException();
        }

        [Fact]
        public void Read_GivenNull_ThenReturnDefault()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

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
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

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
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

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
            Assert.Same(_mockOpenIdMessageContext.Object, message!.Context);
            Assert.Empty(message!);
        }

        [Fact]
        public void Read_GivenNull_ThenValid()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var typeToConvert = typeof(TestOpenIdMessage);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            const string propertyName = "propertyName";

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteNullValue();
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(message);
            Assert.Same(_mockOpenIdMessageContext.Object, message!.Context);

            var (key, value) = Assert.Single(message!.Parameters);
            Assert.Equal(propertyName, key);
            Assert.Equal(propertyName, value.Descriptor.ParameterName);
            Assert.Equal(StringValues.Empty, value.StringValues);
            Assert.Null(value.ParsedValue);
        }

        [Fact]
        public void Read_GivenString_ThenValid()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var typeToConvert = typeof(TestOpenIdMessage);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            const string propertyName = "propertyName";
            const string expectedValue = "value1";

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteStringValue(expectedValue);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(message);
            Assert.Same(_mockOpenIdMessageContext.Object, message!.Context);

            var (key, value) = Assert.Single(message!.Parameters);
            Assert.Equal(propertyName, key);
            Assert.Equal(propertyName, value.Descriptor.ParameterName);
            Assert.Equal(expectedValue, value.StringValues);
            Assert.Null(value.ParsedValue);
        }

        [Fact]
        public void Read_GivenStringList_ThenValid()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var typeToConvert = typeof(TestOpenIdMessage);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            const string propertyName = "propertyName";
            var expectedValue = new[] { "value1", "value2" };

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteStringValue(string.Join(OpenIdConstants.ParameterSeparator, expectedValue));
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(message);
            Assert.Same(_mockOpenIdMessageContext.Object, message!.Context);

            var (key, value) = Assert.Single(message!.Parameters);
            Assert.Equal(propertyName, key);
            Assert.Equal(propertyName, value.Descriptor.ParameterName);
            Assert.Equal(expectedValue, value.StringValues);
            Assert.Null(value.ParsedValue);
        }

        [Fact]
        public void Read_GivenNumberAsDecimal_ThenValid()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var typeToConvert = typeof(TestOpenIdMessage);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            const string propertyName = "propertyName";
            const decimal expectedValue = 3.1415m;
            var expectedValueAsString = expectedValue.ToString(CultureInfo.InvariantCulture);

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteNumberValue(expectedValue);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(message);
            Assert.Same(_mockOpenIdMessageContext.Object, message!.Context);

            var (key, value) = Assert.Single(message!.Parameters);
            Assert.Equal(propertyName, key);
            Assert.Equal(propertyName, value.Descriptor.ParameterName);
            Assert.Equal(expectedValueAsString, value.StringValues);
            Assert.Null(value.ParsedValue);
        }

        [Fact]
        public void Read_GivenNumberAsInt32_ThenValid()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var typeToConvert = typeof(TestOpenIdMessage);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            const string propertyName = "propertyName";
            const int expectedValue = 3;
            var expectedValueAsString = expectedValue.ToString(CultureInfo.InvariantCulture);

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteNumberValue(expectedValue);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(message);
            Assert.Same(_mockOpenIdMessageContext.Object, message!.Context);

            var (key, value) = Assert.Single(message!.Parameters);
            Assert.Equal(propertyName, key);
            Assert.Equal(propertyName, value.Descriptor.ParameterName);
            Assert.Equal(expectedValueAsString, value.StringValues);
            Assert.Null(value.ParsedValue);
        }

        [Fact]
        public void Read_GivenBooleanAsTrue_ThenValid()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var typeToConvert = typeof(TestOpenIdMessage);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            const string propertyName = "propertyName";
            const bool expectedValue = true;
            var expectedValueAsString = expectedValue.ToString(CultureInfo.InvariantCulture);

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteBooleanValue(expectedValue);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(message);
            Assert.Same(_mockOpenIdMessageContext.Object, message!.Context);

            var (key, value) = Assert.Single(message!.Parameters);
            Assert.Equal(propertyName, key);
            Assert.Equal(propertyName, value.Descriptor.ParameterName);
            Assert.Equal(expectedValueAsString, value.StringValues);
            Assert.Null(value.ParsedValue);
        }

        [Fact]
        public void Read_GivenBooleanAsFalse_ThenValid()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var typeToConvert = typeof(TestOpenIdMessage);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            const string propertyName = "propertyName";
            const bool expectedValue = false;
            var expectedValueAsString = expectedValue.ToString(CultureInfo.InvariantCulture);

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteBooleanValue(expectedValue);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(message);
            Assert.Same(_mockOpenIdMessageContext.Object, message!.Context);

            var (key, value) = Assert.Single(message!.Parameters);
            Assert.Equal(propertyName, key);
            Assert.Equal(propertyName, value.Descriptor.ParameterName);
            Assert.Equal(expectedValueAsString, value.StringValues);
            Assert.Null(value.ParsedValue);
        }

        [Fact]
        public void Read_GivenNestedArray_ThenValid()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var typeToConvert = typeof(TestOpenIdMessage);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            const string propertyName = "propertyName";
            const string stringValue = "value1";
            var expectedValue = JsonSerializer.Serialize(new[] { stringValue });

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
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
            Assert.Same(_mockOpenIdMessageContext.Object, message!.Context);

            var (key, value) = Assert.Single(message!.Parameters);
            Assert.Equal(propertyName, key);
            Assert.Equal(propertyName, value.Descriptor.ParameterName);
            Assert.Equal(expectedValue, value.StringValues);
            Assert.Equal(expectedValue, JsonSerializer.Serialize(value.ParsedValue));
        }

        [Fact]
        public void Read_GivenNestedObject_ThenValid()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var typeToConvert = typeof(TestOpenIdMessage);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            const string propertyName = "propertyName";
            const string nestedStringValue = "value1";
            var expectedValue = JsonSerializer.Serialize(new TestNestedObject
                { NestedPropertyName1 = nestedStringValue });

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
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
            Assert.Same(_mockOpenIdMessageContext.Object, message!.Context);

            var (key, value) = Assert.Single(message!.Parameters);
            Assert.Equal(propertyName, key);
            Assert.Equal(propertyName, value.Descriptor.ParameterName);
            Assert.Equal(expectedValue, value.StringValues);
            Assert.Equal(expectedValue, JsonSerializer.Serialize(value.ParsedValue));
        }

        [Fact]
        public void Read_GivenNestedObject_WithJsonConverter_ThenValid()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var typeToConvert = typeof(TestOpenIdMessage);
            var jsonSerializerOptions = new JsonSerializerOptions
            {
                Converters = { new TestNestedObjectJsonConverter() }
            };

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            // known parameters must have unique names
            var parameterName = Guid.NewGuid().ToString("N");

            const bool optional = false;
            const bool allowMultipleValues = false;
            const string nestedStringValue = "value1";

            var knownParameter = new KnownParameter<string?>(
                parameterName,
                optional,
                allowMultipleValues,
                ParameterParsers.String);
            KnownParameters.Register(knownParameter);

            var expectedValue = JsonSerializer.Serialize(new TestNestedObject
                { NestedPropertyName1 = nestedStringValue });

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
            Assert.Same(_mockOpenIdMessageContext.Object, message!.Context);

            var (key, value) = Assert.Single(message!.Parameters);
            Assert.Equal(parameterName, key);
            Assert.Equal(parameterName, value.Descriptor.ParameterName);
            Assert.Equal(expectedValue, value.StringValues);
            Assert.Equal(expectedValue, JsonSerializer.Serialize(value.ParsedValue));
        }

        [Fact]
        public void Read_GivenLoadFailure_ThenThrows()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var typeToConvert = typeof(TestOpenIdMessage);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            // known parameters must have unique names
            var parameterName = Guid.NewGuid().ToString("N");

            const bool optional = false;
            const bool allowMultipleValues = false;

            var knownParameter = new KnownParameter<string?>(
                parameterName,
                optional,
                allowMultipleValues,
                ParameterParsers.String);
            KnownParameters.Register(knownParameter);

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
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

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
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var message = new TestOpenIdMessage();

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
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var message = new TestOpenIdMessage
            {
                Context = _mockOpenIdMessageContext.Object
            };

            const string parameterName = "parameterName";
            const string stringValue = "stringValue";
            message.LoadParameter(parameterName, stringValue);

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
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var message = new TestOpenIdMessage
            {
                Context = _mockOpenIdMessageContext.Object
            };

            const string parameterName = "parameterName";
            var stringValues = new[] { "value1", "value2" };
            message.LoadParameter(parameterName, stringValues);

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
}
