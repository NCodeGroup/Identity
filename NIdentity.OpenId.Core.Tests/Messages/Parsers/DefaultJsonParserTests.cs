using System.Buffers;
using System.Text.Json;
using Moq;
using NIdentity.OpenId.Messages;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;
using Xunit;

namespace NIdentity.OpenId.Core.Tests.Messages.Parsers;

public class DefaultJsonParserTests : IDisposable
{
    private MockRepository MockRepository { get; }
    private Mock<IOpenIdMessageContext> MockOpenIdMessageContext { get; }

    public DefaultJsonParserTests()
    {
        MockRepository = new MockRepository(MockBehavior.Strict);
        MockOpenIdMessageContext = MockRepository.Create<IOpenIdMessageContext>();
    }

    public void Dispose()
    {
        MockRepository.Verify();
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

        var context = MockOpenIdMessageContext.Object;
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
