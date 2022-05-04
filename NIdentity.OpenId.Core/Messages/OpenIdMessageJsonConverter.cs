#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;

namespace NIdentity.OpenId.Messages;

internal class OpenIdMessageJsonConverter<T> : JsonConverter<T?>
    where T : OpenIdMessage<T>, new()
{
    private IOpenIdMessageContext Context { get; }

    public OpenIdMessageJsonConverter(IOpenIdMessageContext context)
    {
        Context = context;
    }

    internal Parameter LoadParameter(string parameterName, ref Utf8JsonReader reader, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray && reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("TODO");

        var descriptor = Context.TryGetKnownParameter(parameterName, out var knownParameter) ?
            new ParameterDescriptor(knownParameter) :
            new ParameterDescriptor(parameterName);

        var jsonParser = descriptor.Loader as IJsonParser ?? DefaultJsonParser.Instance;
        var parameter = jsonParser.Load(Context, descriptor, ref reader, options);

        if (reader.TokenType != JsonTokenType.EndArray && reader.TokenType != JsonTokenType.EndObject)
            throw new JsonException("TODO");

        return parameter;
    }

    public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return default;

        if (reader.TokenType != JsonTokenType.StartObject)
            throw new JsonException("TODO");

        var parameters = new List<Parameter>();

        while (reader.Read())
        {
            string parameterName;

            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (reader.TokenType)
            {
                case JsonTokenType.EndObject:
                    return OpenIdMessage<T>.Load(Context, parameters);

                case JsonTokenType.PropertyName:
                    parameterName = reader.GetString() ?? throw new JsonException("TODO");
                    break;

                default:
                    throw new JsonException("TODO");
            }

            if (!reader.Read())
                throw new JsonException("TODO");

            Parameter parameter;
            // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
            switch (reader.TokenType)
            {
                case JsonTokenType.Null:
                    parameter = Parameter.Load(Context, parameterName, StringValues.Empty);
                    break;

                case JsonTokenType.String:
                    parameter = Parameter.Load(Context, parameterName, reader.GetString()?.Split(OpenIdConstants.ParameterSeparator) ?? Array.Empty<string>());
                    break;

                case JsonTokenType.Number:
                    parameter = Parameter.Load(Context, parameterName, reader.GetDecimal().ToString(CultureInfo.InvariantCulture));
                    break;

                case JsonTokenType.True:
                case JsonTokenType.False:
                    parameter = Parameter.Load(Context, parameterName, reader.GetBoolean().ToString(CultureInfo.InvariantCulture));
                    break;

                case JsonTokenType.StartArray:
                case JsonTokenType.StartObject:
                    parameter = LoadParameter(parameterName, ref reader, options);
                    break;

                default:
                    throw new JsonException("TODO");
            }

            parameters.Add(parameter);
        }

        throw new JsonException("TODO");
    }

    public override void Write(Utf8JsonWriter writer, T? message, JsonSerializerOptions options)
    {
        if (message == null)
        {
            writer.WriteNullValue();
            return;
        }

        writer.WriteStartObject();

        foreach (var parameter in message.Parameters.Values)
        {
            // TODO: use ParameterLoader/Parser to serialize
            var stringValue = string.Join(OpenIdConstants.ParameterSeparator, parameter.StringValues.AsEnumerable());
            writer.WriteString(parameter.Descriptor.ParameterName, stringValue);
        }

        writer.WriteEndObject();
    }
}
