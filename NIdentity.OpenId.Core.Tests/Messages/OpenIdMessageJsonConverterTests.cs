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

            var result = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.Null(result);
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

            var result = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(result);
            Assert.Empty(result);
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

            var result = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(result);

            var actualValue = Assert.Single(result, kvp => kvp.Key == propertyName).Value;
            Assert.Equal(StringValues.Empty, actualValue);
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

            var result = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(result);

            var actualValue = Assert.Single(result, kvp => kvp.Key == propertyName).Value;
            Assert.Equal(expectedValue, actualValue);
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

            var result = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(result);

            var actualValue = Assert.Single(result, kvp => kvp.Key == propertyName).Value;
            Assert.Equal(expectedValue, actualValue);
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

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteNumberValue(expectedValue);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var result = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(result);

            var actualValue = Assert.Single(result, kvp => kvp.Key == propertyName).Value;
            Assert.Equal(expectedValue.ToString(CultureInfo.InvariantCulture), actualValue);
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

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteNumberValue(expectedValue);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var result = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(result);

            var actualValue = Assert.Single(result, kvp => kvp.Key == propertyName).Value;
            Assert.Equal(expectedValue.ToString(CultureInfo.InvariantCulture), actualValue);
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

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteBooleanValue(expectedValue);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var result = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(result);

            var actualValue = Assert.Single(result, kvp => kvp.Key == propertyName).Value;
            Assert.Equal(expectedValue.ToString(CultureInfo.InvariantCulture), actualValue);
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

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteBooleanValue(expectedValue);
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var result = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(result);

            var actualValue = Assert.Single(result, kvp => kvp.Key == propertyName).Value;
            Assert.Equal(expectedValue.ToString(CultureInfo.InvariantCulture), actualValue);
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

            var result = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(result);

            var actualJson = Assert.Single(result, kvp => kvp.Key == propertyName).Value;
            var expectedJson = JsonSerializer.Serialize(new[] { stringValue });
            Assert.Equal(expectedJson, actualJson);
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

            var result = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(result);

            var actualJson = Assert.Single(result, kvp => kvp.Key == propertyName).Value;
            var expectedJson = JsonSerializer.Serialize(new TestNestedObject { NestedPropertyName1 = nestedStringValue });
            Assert.Equal(expectedJson, actualJson);
        }

        [Fact]
        public void Read_GivenLoadFailure_ThenError()
        {
            var converter = new OpenIdMessageJsonConverter<TestOpenIdMessage>(_mockOpenIdMessageContext.Object);

            var typeToConvert = typeof(TestOpenIdMessage);
            var jsonSerializerOptions = new JsonSerializerOptions();

            var bufferWriter = new ArrayBufferWriter<byte>();
            var jsonWriter = new Utf8JsonWriter(bufferWriter);

            // known parameters must have unique names
            var propertyName = Guid.NewGuid().ToString("N");

            const bool optional = false;
            const bool allowMultipleValues = false;

            var knownParameter = new KnownParameter<string>(propertyName, optional, allowMultipleValues, ParameterParsers.String);
            KnownParameters.Register(knownParameter);

            var errors = new List<ValidationResult>();
            _mockOpenIdMessageContext
                .Setup(_ => _.Errors)
                .Returns(errors)
                .Verifiable();

            jsonWriter.WriteStartObject();
            jsonWriter.WritePropertyName(propertyName);
            jsonWriter.WriteNullValue();
            jsonWriter.WriteEndObject();
            jsonWriter.Flush();

            var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

            Assert.True(reader.Read());
            Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

            var result = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
            Assert.NotNull(result);

            var error = Assert.Single(errors);
            Assert.True(error.HasError);
            Assert.NotNull(error.ErrorDetails);
        }
    }
}
