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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using NCode.Identity.OpenId.Tests.Messages.Parsers;
using Xunit;

namespace NCode.Identity.OpenId.Tests.Messages;

public class OpenIdMessageJsonConverterTests : BaseTests
{
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }

    public OpenIdMessageJsonConverterTests()
    {
        MockOpenIdEnvironment = CreateStrictMock<OpenIdEnvironment>();
    }

    [Fact]
    public void ReadParameter_ThenValid()
    {
        const string parameterName = "parameterName";
        const SerializationFormat format = SerializationFormat.OpenId;

        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        var reader = new Utf8JsonReader(Span<byte>.Empty);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        var loader = new ProxyParameterLoader();
        var descriptor = new ParameterDescriptor(parameterName, loader);

        var mockParameter = CreateStrictMock<IParameter>();

        // ReSharper disable ParameterOnlyUsedForPreconditionCheck.Local
        loader.ReadCallback = (localEnvironment, localDescriptor, localFormat, localOptions) =>
        {
            Assert.Same(MockOpenIdEnvironment.Object, localEnvironment);
            Assert.Equal(parameterName, localDescriptor.ParameterName);
            Assert.Same(loader, localDescriptor.Loader);
            Assert.Equal(format, localFormat);
            Assert.Same(jsonSerializerOptions, localOptions);
            return mockParameter.Object;
        };
        // ReSharper restore ParameterOnlyUsedForPreconditionCheck.Local

        MockOpenIdEnvironment
            .Setup(x => x.GetParameterDescriptor(parameterName))
            .Returns(descriptor)
            .Verifiable();

        var parameter = converter.ReadParameter(ref reader, parameterName, format, jsonSerializerOptions);
        Assert.Same(mockParameter.Object, parameter);
    }

    [Fact]
    public void CreateMessage_ThenValid()
    {
        const string typeDiscriminator = nameof(typeDiscriminator);

        var parameters = Array.Empty<IParameter>();
        var mockMessage = CreateStrictMock<IOpenIdMessage>();
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        MockOpenIdEnvironment
            .Setup(x => x.CreateMessage(typeDiscriminator, parameters))
            .Returns(mockMessage.Object)
            .Verifiable();

        var result = converter.CreateMessage(typeDiscriminator, parameters);
        Assert.Same(mockMessage.Object, result);
    }

    [Fact]
    public void CreateMessage_InvalidType_ThenThrows()
    {
        const string typeDiscriminator = nameof(typeDiscriminator);

        var parameters = Array.Empty<IParameter>();
        var mockMessage = CreateStrictMock<IOpenIdMessage>();
        var converter = new OpenIdMessageJsonConverter<ITestOpenIdMessage>(MockOpenIdEnvironment.Object);

        MockOpenIdEnvironment
            .Setup(x => x.CreateMessage(typeDiscriminator, parameters))
            .Returns(mockMessage.Object)
            .Verifiable();

        Assert.Throws<InvalidOperationException>(() =>
            converter.CreateMessage(typeDiscriminator, parameters)
        );
    }

    [Fact]
    public void Read_GivenNullEnvelope_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        var typeToConvert = typeof(IOpenIdMessage);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

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
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        var typeToConvert = typeof(IOpenIdMessage);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

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
    public void Read_GivenEndObject_ThenNoProperties()
    {
        const string typeDiscriminator = nameof(OpenIdMessage);

        var mockMessage = CreateStrictMock<IOpenIdMessage>();
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        var typeToConvert = typeof(IOpenIdMessage);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        jsonWriter.WriteStartObject();
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        mockMessage
            .SetupSet(x => x.SerializationFormat = SerializationFormat.OpenId)
            .Verifiable();

        MockOpenIdEnvironment
            .Setup(x => x.CreateMessage(
                typeDiscriminator,
                It.Is<IEnumerable<IParameter>>(parameters => !parameters.Any()))
            )
            .Returns(mockMessage.Object)
            .Verifiable();

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.Same(mockMessage.Object, message);
    }

    [Fact]
    public void Read_GivenNullProperty_ThenValid()
    {
        const string typeDiscriminator = nameof(OpenIdMessage);
        const string parameterName = nameof(parameterName);

        var mockMessage = CreateStrictMock<IOpenIdMessage>();
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        var typeToConvert = typeof(IOpenIdMessage);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        var mockParameter = CreateStrictMock<IParameter>();
        var loader = new ProxyParameterLoader
        {
            ReadCallback = (_, _, _, _) => mockParameter.Object
        };
        var parameterDescriptor = new ParameterDescriptor(parameterName, loader);

        MockOpenIdEnvironment
            .Setup(x => x.GetParameterDescriptor(parameterName))
            .Returns(parameterDescriptor)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteNullValue();
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        mockMessage
            .SetupSet(x => x.SerializationFormat = SerializationFormat.OpenId)
            .Verifiable();

        MockOpenIdEnvironment
            .Setup(x => x.CreateMessage(
                typeDiscriminator,
                It.Is<IEnumerable<IParameter>>(parameters => parameters.Single() == mockParameter.Object))
            )
            .Returns(mockMessage.Object)
            .Verifiable();

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.Same(mockMessage.Object, message);
    }

    [Fact]
    public void Read_GivenTypeMetadata_ThenValid()
    {
        const string parameterName = nameof(parameterName);
        const string parameterValue = nameof(parameterValue);
        const string typeDiscriminator = nameof(typeDiscriminator);

        var mockMessage = CreateStrictMock<IOpenIdMessage>();
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        var typeToConvert = typeof(IOpenIdMessage);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        var mockParameter = CreateStrictMock<IParameter>();
        var loader = new ProxyParameterLoader
        {
            ReadCallback = (_, _, _, _) => mockParameter.Object
        };
        var parameterDescriptor = new ParameterDescriptor(parameterName, loader);

        MockOpenIdEnvironment
            .Setup(x => x.GetParameterDescriptor(parameterName))
            .Returns(parameterDescriptor)
            .Verifiable();

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName("$type");
        jsonWriter.WriteStringValue(typeDiscriminator);
        jsonWriter.WritePropertyName(parameterName);
        jsonWriter.WriteStringValue(parameterValue);
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        mockMessage
            .SetupSet(x => x.SerializationFormat = SerializationFormat.OpenId)
            .Verifiable();

        MockOpenIdEnvironment
            .Setup(x => x.CreateMessage(
                typeDiscriminator,
                It.Is<IEnumerable<IParameter>>(parameters => parameters.Single() == mockParameter.Object))
            )
            .Returns(mockMessage.Object)
            .Verifiable();

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.Same(mockMessage.Object, message);
    }

    [Fact]
    public void Read_GivenFormatMetadata_ThenJson()
    {
        const string typeDiscriminator = nameof(OpenIdMessage);

        var mockMessage = CreateStrictMock<IOpenIdMessage>();
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        var typeToConvert = typeof(IOpenIdMessage);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName("$format");
        jsonWriter.WriteStringValue(SerializationFormat.Json.ToString());
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        mockMessage
            .SetupSet(x => x.SerializationFormat = SerializationFormat.Json)
            .Verifiable();

        MockOpenIdEnvironment
            .Setup(x => x.CreateMessage(
                typeDiscriminator,
                It.Is<IEnumerable<IParameter>>(parameters => !parameters.Any()))
            )
            .Returns(mockMessage.Object)
            .Verifiable();

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.Same(mockMessage.Object, message);
    }

    [Fact]
    public void Read_GivenFormatMetadata_ThenOpenId()
    {
        const string typeDiscriminator = nameof(OpenIdMessage);

        var mockMessage = CreateStrictMock<IOpenIdMessage>();
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        var typeToConvert = typeof(IOpenIdMessage);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);

        jsonWriter.WriteStartObject();
        jsonWriter.WritePropertyName("$format");
        jsonWriter.WriteStringValue(SerializationFormat.OpenId.ToString());
        jsonWriter.WriteEndObject();
        jsonWriter.Flush();

        var reader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(reader.Read());
        Assert.Equal(JsonTokenType.StartObject, reader.TokenType);

        mockMessage
            .SetupSet(x => x.SerializationFormat = SerializationFormat.OpenId)
            .Verifiable();

        MockOpenIdEnvironment
            .Setup(x => x.CreateMessage(
                typeDiscriminator,
                It.Is<IEnumerable<IParameter>>(parameters => !parameters.Any()))
            )
            .Returns(mockMessage.Object)
            .Verifiable();

        var message = converter.Read(ref reader, typeToConvert, jsonSerializerOptions);
        Assert.Same(mockMessage.Object, message);
    }

    [Fact]
    public void Write_GivenNull_ThenValid()
    {
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        const OpenIdMessage? message = null;

        converter.Write(jsonWriter, message, jsonSerializerOptions);
        jsonWriter.Flush();

        var jsonReader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.Null, jsonReader.TokenType);
    }

    [Fact]
    public void Write_GivenEmpty_FormatJson_ThenValid()
    {
        const string typeDiscriminator = nameof(OpenIdMessage);

        var environment = MockOpenIdEnvironment.Object;
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(environment);

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        var message = new OpenIdMessage(environment, Array.Empty<Parameter>());

        Assert.Equal(SerializationFormat.Json, message.SerializationFormat);

        converter.Write(jsonWriter, message, jsonSerializerOptions);
        jsonWriter.Flush();

        var jsonReader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        // {

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.StartObject, jsonReader.TokenType);

        // $type=TestOpenIdMessage

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal("$type", jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(typeDiscriminator, jsonReader.GetString());

        // $format=json

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal("$format", jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal("json", jsonReader.GetString());

        // }

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);
    }

    [Fact]
    public void Write_GivenEmpty_FormatOpenId_ThenValid()
    {
        var environment = MockOpenIdEnvironment.Object;
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(environment);

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        var message = new OpenIdMessage(environment, Array.Empty<Parameter>())
        {
            SerializationFormat = SerializationFormat.OpenId
        };

        converter.Write(jsonWriter, message, jsonSerializerOptions);
        jsonWriter.Flush();

        var jsonReader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        // {

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.StartObject, jsonReader.TokenType);

        // }

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);
    }

    [Fact]
    public void Write_GivenStringValue_FormatJson_ThenValid()
    {
        const string typeDiscriminator = nameof(OpenIdMessage);

        var environment = MockOpenIdEnvironment.Object;
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(environment);

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        const string parameterName = nameof(parameterName);
        const string parsedValue = nameof(parsedValue);

        var parser = ParameterParsers.String;
        var descriptor = new ParameterDescriptor(parameterName, parser);
        var parameter = new Parameter<string?>(descriptor, parser, parsedValue);

        var message = new OpenIdMessage(environment, [parameter]);

        Assert.Equal(SerializationFormat.Json, message.SerializationFormat);

        converter.Write(jsonWriter, message, jsonSerializerOptions);
        jsonWriter.Flush();

        var jsonReader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        // {

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.StartObject, jsonReader.TokenType);

        // $type=TestOpenIdMessage

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal("$type", jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(typeDiscriminator, jsonReader.GetString());

        // $format=json

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal("$format", jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal("json", jsonReader.GetString());

        // parameterName=stringValue

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal(parameterName, jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(parsedValue, jsonReader.GetString());

        // }

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);
    }

    [Fact]
    public void Write_GivenStringValue_FormatOpenId_ThenValid()
    {
        var environment = MockOpenIdEnvironment.Object;
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(environment);

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        const string parameterName = nameof(parameterName);
        const string parsedValue = nameof(parsedValue);

        var parser = ParameterParsers.String;
        var descriptor = new ParameterDescriptor(parameterName, parser);
        var parameter = new Parameter<string?>(descriptor, parser, parsedValue);

        var message = new OpenIdMessage(environment, [parameter])
        {
            SerializationFormat = SerializationFormat.OpenId
        };

        converter.Write(jsonWriter, message, jsonSerializerOptions);
        jsonWriter.Flush();

        var jsonReader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        // {

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.StartObject, jsonReader.TokenType);

        // parameterName=stringValue

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal(parameterName, jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(parsedValue, jsonReader.GetString());

        // }

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);
    }

    [Fact]
    public void Write_GivenStringList_FormatJson_ThenValid()
    {
        const string typeDiscriminator = nameof(OpenIdMessage);

        var environment = MockOpenIdEnvironment.Object;
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(environment);

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        const string parameterName = nameof(parameterName);
        var parsedValue = new HashSet<string> { "value1", "value2" };

        var parser = ParameterParsers.StringSet;
        var descriptor = new ParameterDescriptor(parameterName, parser);
        var parameter = new Parameter<HashSet<string>?>(descriptor, parser, parsedValue);

        var message = new OpenIdMessage(environment, [parameter]);

        Assert.Equal(SerializationFormat.Json, message.SerializationFormat);

        converter.Write(jsonWriter, message, jsonSerializerOptions);
        jsonWriter.Flush();

        var jsonReader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        // {

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.StartObject, jsonReader.TokenType);

        // $type=TestOpenIdMessage

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal("$type", jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(typeDiscriminator, jsonReader.GetString());

        // $format=json

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal("$format", jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal("json", jsonReader.GetString());

        // parameterName=[value1, value2]

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal(parameterName, jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.StartArray, jsonReader.TokenType);

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(parsedValue.ElementAt(0), jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(parsedValue.ElementAt(1), jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.EndArray, jsonReader.TokenType);

        // }

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);
    }

    [Fact]
    public void Write_GivenStringList_FormatOpenId_ThenValid()
    {
        var environment = MockOpenIdEnvironment.Object;
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(environment);

        var bufferWriter = new ArrayBufferWriter<byte>();
        var jsonWriter = new Utf8JsonWriter(bufferWriter);
        var jsonSerializerOptions = JsonSerializerOptions.Web;

        const string parameterName = nameof(parameterName);
        var parsedValue = new HashSet<string> { "value1", "value2" };

        var parser = ParameterParsers.StringSet;
        var descriptor = new ParameterDescriptor(parameterName, parser);
        var parameter = new Parameter<HashSet<string>?>(descriptor, parser, parsedValue);

        var message = new OpenIdMessage(environment, [parameter])
        {
            SerializationFormat = SerializationFormat.OpenId
        };

        converter.Write(jsonWriter, message, jsonSerializerOptions);
        jsonWriter.Flush();

        var jsonReader = new Utf8JsonReader(bufferWriter.WrittenSpan);

        // {

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.StartObject, jsonReader.TokenType);

        // parameterName="value1 value2"

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal(parameterName, jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(string.Join(' ', parsedValue), jsonReader.GetString());

        // }

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);
    }
}
