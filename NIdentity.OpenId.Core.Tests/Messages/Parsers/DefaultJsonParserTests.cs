using System;
using System.Buffers;
using System.Text.Json;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers
{
    public class DefaultJsonParserTests : IDisposable
    {
        private readonly MockRepository _mockRepository;
        private readonly Mock<IOpenIdMessageContext> _mockOpenIdMessageContext;

        public DefaultJsonParserTests()
        {
            _mockRepository = new MockRepository(MockBehavior.Strict);
            _mockOpenIdMessageContext = _mockRepository.Create<IOpenIdMessageContext>();
        }

        public void Dispose()
        {
            _mockRepository.Verify();
        }

        [Fact]
        public void Singleton()
        {
            var parser = DefaultJsonParser.Instance;

            Assert.Same(DefaultJsonParser.Instance, parser);
        }

        [Fact]
        public void Load_ThenIsValid()
        {
            var parser = new DefaultJsonParser();

            const string parameterName = "parameterName";

            var context = _mockOpenIdMessageContext.Object;
            var jsonSerializerOptions = new JsonSerializerOptions();

            var descriptor = new ParameterDescriptor(parameterName);

            var buffer = new ArrayBufferWriter<byte>();
            var writer = new Utf8JsonWriter(buffer);

            var expectedValue = new TestNestedObject { NestedPropertyName1 = "NestedPropertyValue" };
            var expectedStringValue = JsonSerializer.Serialize(expectedValue);
            JsonSerializer.Serialize(writer, expectedValue);

            var reader = new Utf8JsonReader(buffer.WrittenSpan);

            var parameter = parser.Load(context, descriptor, ref reader, jsonSerializerOptions);

            Assert.Equal(expectedStringValue, parameter.StringValues);
            Assert.IsType<JsonElement>(parameter.ParsedValue);
            Assert.Equal(expectedStringValue, JsonSerializer.Serialize(parameter.ParsedValue));
        }
    }
}
