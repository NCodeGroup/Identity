using System;
using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers
{
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
            var parameter = new Parameter(descriptor);

            var buffer = new ArrayBufferWriter<byte>();
            var writer = new Utf8JsonWriter(buffer);
            converter.Write(writer, expectedValue, jsonSerializerOptions);
            writer.Flush();

            var reader = new Utf8JsonReader(buffer.WrittenSpan);

            Assert.True(reader.Read());

            parser.Load(context, parameter, ref reader, jsonSerializerOptions);

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
        public void TryParse_GivenEmpty_WhenOptional_ThenSuccess()
        {
            var parser = new JsonParser<TestNestedObject>();

            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, optional: true, allowMultipleValues: false, parser);
            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(context, descriptor, StringValues.Empty, out var result);
            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Null(result.ErrorDetails);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenEmpty_WhenRequired_ThenError()
        {
            var parser = new JsonParser<TestNestedObject>();

            var context = _mockOpenIdMessageContext.Object;

            const string parameterName = "parameterName";
            var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, optional: false, allowMultipleValues: false, parser);
            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(context, descriptor, StringValues.Empty, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.NotNull(result.ErrorDetails);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenMultipleValues_ThenError()
        {
            var parser = new JsonParser<TestNestedObject>();

            var context = _mockOpenIdMessageContext.Object;

            var stringValues = new[] { "value1", "value2" };

            const string parameterName = "parameterName";
            var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, optional: false, allowMultipleValues: false, parser);
            var descriptor = new ParameterDescriptor(knownParameter);

            var success = parser.TryParse(context, descriptor, stringValues, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.NotNull(result.ErrorDetails);
            Assert.Null(result.Value);
        }

        [Fact]
        public void TryParse_GivenValidJson_ThenSuccess()
        {
            var parser = new JsonParser<TestNestedObject>();

            var context = _mockOpenIdMessageContext.Object;
            var jsonSerializerOptions = new JsonSerializerOptions();

            _mockOpenIdMessageContext
                .Setup(_ => _.JsonSerializerOptions)
                .Returns(jsonSerializerOptions)
                .Verifiable();

            const string parameterName = "parameterName";
            var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, optional: false, allowMultipleValues: false, parser);
            var descriptor = new ParameterDescriptor(knownParameter);

            var expectedValue = new TestNestedObject { NestedPropertyName1 = "NestedPropertyValue" };
            var expectedValueAsJson = JsonSerializer.Serialize(expectedValue);

            var success = parser.TryParse(context, descriptor, expectedValueAsJson, out var result);
            Assert.True(success);
            Assert.False(result.HasError);
            Assert.Null(result.ErrorDetails);
            Assert.Equal(expectedValueAsJson, JsonSerializer.Serialize(result.Value));
        }

        [Fact]
        public void TryParse_GivenInvalidJson_ThenError()
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
            var knownParameter = new KnownParameter<TestNestedObject?>(parameterName, optional: false, allowMultipleValues: false, parser);
            var descriptor = new ParameterDescriptor(knownParameter);

            const string stringValue = "@invalid_json$";

            var success = parser.TryParse(context, descriptor, stringValue, out var result);
            Assert.False(success);
            Assert.True(result.HasError);
            Assert.NotNull(result.ErrorDetails);
            Assert.Null(result.Value);
        }
    }
}
