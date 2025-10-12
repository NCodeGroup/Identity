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

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="IOpenIdMessage"/>
/// instances to and from JSON.
/// </summary>
/// <typeparam name="T">The type of the <see cref="IOpenIdMessage"/> instance to serialize and deserialize.</typeparam>
[PublicAPI]
public class OpenIdMessageJsonConverter<T>(
    OpenIdEnvironment openIdEnvironment
) : JsonConverter<T?>
    where T : class, IOpenIdMessage
{
    private const string TypeKey = "$type";
    private const string FormatKey = "$format";

    // ReSharper disable once StaticMemberInGenericType
    private static string DefaultTypeDiscriminator { get; } = GetDefaultTypeDiscriminator();

    private OpenIdEnvironment OpenIdEnvironment { get; } = openIdEnvironment;

    private static string GetDefaultTypeDiscriminator()
    {
        var type = typeof(T);
        var attribute = type.GetCustomAttribute<OpenIdTypeDiscriminatorAttribute>();
        return attribute?.TypeDiscriminator ?? (type.IsClass ? type.Name : nameof(OpenIdMessage));
    }

    internal IParameter ReadParameter(
        ref Utf8JsonReader reader,
        string parameterName,
        SerializationFormat format,
        JsonSerializerOptions options)
    {
        var descriptor = OpenIdEnvironment.GetParameterDescriptor(parameterName);
        return descriptor.Loader.Read(ref reader, OpenIdEnvironment, descriptor, format, options);
    }

    internal virtual T CreateMessage(string typeDiscriminator, IEnumerable<IParameter> parameters)
    {
        var message = OpenIdEnvironment.CreateMessage(typeDiscriminator, parameters);
        if (message is not T typedMessage)
            throw new InvalidOperationException();

        return typedMessage;
    }

    /// <inheritdoc />
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        T? messageOrNull = null;
        var typeDiscriminator = DefaultTypeDiscriminator;

        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        const SerializationFormat defaultFormat = SerializationFormat.OpenId;
        SerializationFormat? formatOrNull = null;

        var parameters = new List<IParameter>();

        while (reader.Read())
        {
            string parameterName;

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (reader.TokenType)
            {
                case JsonTokenType.Comment:
                    continue;

                case JsonTokenType.EndObject:
                    messageOrNull ??= CreateMessage(typeDiscriminator, parameters);
                    messageOrNull.SerializationFormat = formatOrNull ?? defaultFormat;
                    return messageOrNull;

                case JsonTokenType.PropertyName:
                    parameterName = reader.GetString() ?? throw new InvalidOperationException();
                    break;

                default:
                    throw new JsonException();
            }

            if (!reader.Read())
                throw new JsonException();

            // ReSharper disable once ConvertIfStatementToSwitchStatement
            if (parameterName == TypeKey)
            {
                if (reader.TokenType != JsonTokenType.String)
                    throw new JsonException();

                typeDiscriminator = reader.GetString();
                if (string.IsNullOrEmpty(typeDiscriminator))
                    throw new JsonException();

                continue;
            }

            if (parameterName == FormatKey)
            {
                var formatName = reader.GetString();
                if (!Enum.TryParse(formatName, ignoreCase: true, out SerializationFormat format))
                {
                    throw new InvalidOperationException("Unable to parse the serialization format.");
                }

                if (formatOrNull.HasValue && formatOrNull.Value != format)
                {
                    throw new InvalidOperationException("The serialization format has already been set and cannot be changed.");
                }

                formatOrNull = format;

                continue;
            }

            // when not specified, default to OpenId serialization (aka string values)
            var effectiveFormat = formatOrNull ??= defaultFormat;

            var parameter = ReadParameter(ref reader, parameterName, effectiveFormat, options);
            parameters.Add(parameter);
        }

        throw new JsonException();
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, T? message, JsonSerializerOptions options)
    {
        if (message == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        var format = message.SerializationFormat;
        var asJson = format == SerializationFormat.Json;

        if (asJson)
        {
            writer.WriteString(TypeKey, message.TypeDiscriminator);
            writer.WriteString(FormatKey, format.ToString().ToLowerInvariant());
        }

        foreach (var parameter in message.Parameters)
        {
            var descriptor = parameter.Descriptor;
            if (!descriptor.ShouldSerialize(OpenIdEnvironment, parameter, format))
                continue;

            descriptor.Loader.Write(writer, OpenIdEnvironment, parameter, format, options);
        }

        writer.WriteEndObject();
    }
}
