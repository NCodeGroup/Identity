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

using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Messages.Parsers;

namespace NIdentity.OpenId.Messages
{
    internal class OpenIdMessageJsonConverter<T> : JsonConverter<T?>
        where T : OpenIdMessage, new()
    {
        private readonly IOpenIdMessageContext _context;

        public OpenIdMessageJsonConverter(IOpenIdMessageContext context)
        {
            _context = context;
        }

        internal void LoadNestedJson(ref Utf8JsonReader reader, JsonSerializerOptions options, T message, string parameterName)
        {
            if (reader.TokenType != JsonTokenType.StartArray && reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("TODO");

            var isNew = false;
            if (!message.Parameters.TryGetValue(parameterName, out var parameter))
            {
                var descriptor = KnownParameters.TryGet(parameterName, out var knownParameter) ?
                    new ParameterDescriptor(knownParameter) :
                    new ParameterDescriptor(parameterName);

                isNew = true;
                parameter = new Parameter(descriptor);
            }

            var jsonParser = parameter.Descriptor.Loader as IJsonParser ?? DefaultJsonParser.Instance;

            jsonParser.Load(_context, parameter, ref reader, options);

            if (reader.TokenType != JsonTokenType.EndArray && reader.TokenType != JsonTokenType.EndObject)
                throw new JsonException("TODO");

            if (isNew)
                message.Parameters[parameterName] = parameter;
        }

        public override T? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return default;

            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException("TODO");

            var message = new T { Context = _context };

            while (reader.Read())
            {
                string parameterName;

                // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                switch (reader.TokenType)
                {
                    case JsonTokenType.EndObject:
                        return message;

                    case JsonTokenType.PropertyName:
                        parameterName = reader.GetString() ?? throw new JsonException("TODO");
                        break;

                    default:
                        throw new JsonException("TODO");
                }

                if (!reader.Read())
                    throw new JsonException("TODO");

                StringValues stringValues;
                // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                switch (reader.TokenType)
                {
                    case JsonTokenType.Null:
                        stringValues = StringValues.Empty;
                        break;

                    case JsonTokenType.String:
                        stringValues = new StringValues(reader.GetString()?.Split(OpenIdConstants.ParameterSeparator) ?? Array.Empty<string>());
                        break;

                    case JsonTokenType.Number:
                        stringValues = new StringValues(reader.GetDecimal().ToString(CultureInfo.InvariantCulture));
                        break;

                    case JsonTokenType.True:
                    case JsonTokenType.False:
                        stringValues = new StringValues(reader.GetBoolean().ToString(CultureInfo.InvariantCulture));
                        break;

                    case JsonTokenType.StartArray:
                    case JsonTokenType.StartObject:
                        LoadNestedJson(ref reader, options, message, parameterName);
                        continue;

                    default:
                        throw new JsonException("TODO");
                }

                message.LoadParameter(parameterName, stringValues);
            }

            throw new JsonException("TODO");
        }

        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();

            foreach (var parameter in value.Parameters.Values)
            {
                // TODO: use ParameterLoader/Parser to serialize
                var stringValue = string.Join(OpenIdConstants.ParameterSeparator, parameter.StringValues.AsEnumerable());
                writer.WriteString(parameter.Descriptor.ParameterName, stringValue);
            }

            writer.WriteEndObject();
        }
    }
}
