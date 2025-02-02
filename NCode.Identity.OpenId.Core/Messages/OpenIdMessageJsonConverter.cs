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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;

namespace NCode.Identity.OpenId.Messages;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="IOpenIdMessage"/>
/// instances to and from JSON.
/// </summary>
/// <typeparam name="T">The type of the <see cref="IOpenIdMessage"/> instance to serialize and deserialize.</typeparam>
public class OpenIdMessageJsonConverter<T>(
    OpenIdEnvironment openIdEnvironment
) : JsonConverter<T?>
    where T : OpenIdMessage
{
    private const string TypeKey = "$type";
    private const string PropertiesKey = "$properties";

    private OpenIdEnvironment OpenIdEnvironment { get; } = openIdEnvironment;

    internal Parameter LoadParameter(string parameterName, ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        var descriptor = OpenIdEnvironment.KnownParameters.TryGet(parameterName, out var knownParameter) ?
            new ParameterDescriptor(knownParameter) :
            new ParameterDescriptor(parameterName);
        var jsonParser = descriptor.Loader as IJsonParser ?? DefaultJsonParser.Singleton;
        return jsonParser.Read(ref reader, OpenIdEnvironment, descriptor, options);
    }

    private static T CreateMessage(Type messageType)
    {
        if (messageType == typeof(IOpenIdMessage))
            messageType = typeof(OpenIdMessage);

        if (!typeof(T).IsAssignableFrom(messageType))
            throw new InvalidOperationException();

        if (!typeof(OpenIdMessage).IsAssignableFrom(messageType))
            throw new InvalidOperationException();

        const BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var hasDefaultConstructor = messageType.GetConstructor(bindingFlags, Type.EmptyTypes) != null;
        if (!hasDefaultConstructor)
            throw new InvalidOperationException();

        const bool nonPublic = true;
        return (T)(Activator.CreateInstance(messageType, nonPublic) ?? throw new InvalidOperationException());
    }

    /// <inheritdoc />
    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        T? messageOrNull = null;
        var messageType = typeof(T);

        if (reader.TokenType == JsonTokenType.Null)
            return default;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException();

        var parameters = new List<Parameter>();

        while (reader.Read())
        {
            string parameterName;

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (reader.TokenType)
            {
                case JsonTokenType.EndObject:
                    messageOrNull ??= CreateMessage(messageType);
                    messageOrNull.Initialize(OpenIdEnvironment, parameters);
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

                var messageTypeName = reader.GetString();
                if (string.IsNullOrEmpty(messageTypeName))
                    throw new JsonException();

                const bool throwOnError = false;
                messageType = Type.GetType(messageTypeName, throwOnError) ?? throw new InvalidOperationException();

                continue;
            }

            if (parameterName == PropertiesKey)
            {
                // TODO: unit tests

                messageOrNull ??= CreateMessage(messageType);

                if (messageOrNull is not ISupportProperties supportProperties)
                {
                    throw new InvalidOperationException();
                }

                supportProperties.DeserializeProperties(ref reader, options);

                continue;
            }

            var parameter = LoadParameter(parameterName, ref reader, options);
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

        // TODO: how to exclude $type and $properties from the output?

        var typeOfMessage = message.GetType();
        writer.WriteString(TypeKey, typeOfMessage.AssemblyQualifiedName);

        if (message is ISupportProperties supportProperties)
        {
            writer.WritePropertyName(PropertiesKey);
            supportProperties.SerializeProperties(writer, options);
        }

        foreach (var parameter in message.Parameters.Values)
        {
            var jsonParser = parameter.Descriptor.Loader as IJsonParser ?? DefaultJsonParser.Singleton;
            jsonParser.Write(writer, OpenIdEnvironment, parameter, options);
        }

        writer.WriteEndObject();
    }
}
