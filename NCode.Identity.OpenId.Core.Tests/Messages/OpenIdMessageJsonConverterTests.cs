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
using Microsoft.Extensions.Primitives;
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

        var message = new OpenIdMessage(environment, Array.Empty<Parameter>())
        {
            SerializationFormat = SerializationFormat.Json
        };

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
        Assert.Equal("Json", jsonReader.GetString());

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
        const string stringValue = nameof(stringValue);

        var parameter = new Parameter<string>
        {
            Descriptor = new ParameterDescriptor(parameterName, ParameterLoader.Default),
            StringValues = stringValue,
            ParsedValue = stringValue
        };
        var message = new OpenIdMessage(environment, [parameter])
        {
            SerializationFormat = SerializationFormat.Json
        };

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
        Assert.Equal("Json", jsonReader.GetString());

        // parameterName=stringValue

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal(parameterName, jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(stringValue, jsonReader.GetString());

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
        const string stringValue = nameof(stringValue);

        var parameter = new Parameter<string>
        {
            Descriptor = new ParameterDescriptor(parameterName, ParameterLoader.Default),
            StringValues = stringValue,
            ParsedValue = stringValue
        };
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
        Assert.Equal(stringValue, jsonReader.GetString());

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
        var stringValues = new[] { "value1", "value2" };

        var parameter = new Parameter<string[]>
        {
            Descriptor = new ParameterDescriptor(parameterName, ParameterLoader.Default),
            StringValues = stringValues,
            ParsedValue = stringValues
        };
        var message = new OpenIdMessage(environment, [parameter])
        {
            SerializationFormat = SerializationFormat.Json
        };

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
        Assert.Equal("Json", jsonReader.GetString());

        // parameterName=[value1, value2]

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.PropertyName, jsonReader.TokenType);
        Assert.Equal(parameterName, jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.StartArray, jsonReader.TokenType);

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(stringValues[0], jsonReader.GetString());

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.String, jsonReader.TokenType);
        Assert.Equal(stringValues[1], jsonReader.GetString());

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
        var stringValues = new[] { "value1", "value2" };

        var message = new OpenIdMessage();
        var parameter = new Parameter<string[]>
        {
            Descriptor = new ParameterDescriptor(parameterName, ParameterLoader.Default),
            StringValues = stringValues,
            ParsedValue = stringValues
        };
        message.Initialize(environment, [parameter]);

        message.SerializationFormat = SerializationFormat.OpenId;

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
        Assert.Equal(string.Join(' ', stringValues), jsonReader.GetString());

        // }

        Assert.True(jsonReader.Read());
        Assert.Equal(JsonTokenType.EndObject, jsonReader.TokenType);
    }

    [Theory]
    [InlineData(SerializationFormats.None, SerializationFormat.Json, true)]
    [InlineData(SerializationFormats.None, SerializationFormat.OpenId, true)]
    [InlineData(SerializationFormats.Json, SerializationFormat.Json, false)]
    [InlineData(SerializationFormats.Json, SerializationFormat.OpenId, true)]
    [InlineData(SerializationFormats.Json | SerializationFormats.OpenId, SerializationFormat.Json, false)]
    [InlineData(SerializationFormats.Json | SerializationFormats.OpenId, SerializationFormat.OpenId, false)]
    public void ShouldSerialize_Valid(SerializationFormats prohibitedFormats, SerializationFormat format, bool expectedResult)
    {
        var environment = MockOpenIdEnvironment.Object;
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(environment);

        var result = converter.ShouldSerialize(prohibitedFormats, format);
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void GetValueToSerialize_FormatJson_Valid()
    {
        const SerializationFormat format = SerializationFormat.Json;
        const string parsedValue = nameof(parsedValue);

        var mockConverter = CreatePartialMock<OpenIdMessageJsonConverter<IOpenIdMessage>>(MockOpenIdEnvironment.Object);
        var mockParameter = CreateStrictMock<IParameter>();

        mockParameter
            .Setup(x => x.GetParsedValue())
            .Returns(parsedValue)
            .Verifiable();

        var converter = mockConverter.Object;

        var result = converter.GetValueToSerialize(mockParameter.Object, format);
        Assert.Equal(parsedValue, result);
    }

    [Fact]
    public void GetValueToSerialize_FormatOpenId_Valid()
    {
        const SerializationFormat format = SerializationFormat.OpenId;
        const string openIdValue = nameof(openIdValue);

        var mockConverter = CreatePartialMock<OpenIdMessageJsonConverter<IOpenIdMessage>>(MockOpenIdEnvironment.Object);

        StringValues stringValues = new[] { "stringValue1", "stringValue2" };
        mockConverter
            .Setup(x => x.GetOpenIdValue(stringValues))
            .Returns(openIdValue)
            .Verifiable();

        var mockParameter = CreateStrictMock<IParameter>();
        mockParameter
            .Setup(x => x.StringValues)
            .Returns(stringValues)
            .Verifiable();

        var converter = mockConverter.Object;

        var result = converter.GetValueToSerialize(mockParameter.Object, format);
        Assert.Equal(openIdValue, result);
    }

    [Fact]
    public void GetOpenIdValue_GivenEmpty_Valid()
    {
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        var stringValues = StringValues.Empty;
        var result = converter.GetOpenIdValue(stringValues);
        Assert.Null(result);
    }

    [Fact]
    public void GetOpenIdValue_GivenSingle_Valid()
    {
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        const string stringValue = nameof(stringValue);
        StringValues stringValues = new[] { stringValue };
        var result = converter.GetOpenIdValue(stringValues);
        Assert.Equal(stringValue, result);
    }

    [Fact]
    public void GetOpenIdValue_GivenMultiple_Valid()
    {
        var converter = new OpenIdMessageJsonConverter<IOpenIdMessage>(MockOpenIdEnvironment.Object);

        var stringValuesArray = new[] { "stringValue1", "stringValue2" };
        StringValues stringValues = stringValuesArray;
        var result = converter.GetOpenIdValue(stringValues);
        Assert.Equal(string.Join(' ', stringValuesArray), result);
    }
}
