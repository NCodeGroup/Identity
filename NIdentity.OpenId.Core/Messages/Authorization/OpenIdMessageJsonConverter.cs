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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Primitives;

namespace NIdentity.OpenId.Messages.Authorization
{
    internal class OpenIdMessageJsonConverter<T> : JsonConverter<T?>
        where T : OpenIdMessage, new()
    {
        private readonly ILogger<OpenIdMessageJsonConverter<T>> _logger;

        public OpenIdMessageJsonConverter()
        {
            _logger = NullLogger<OpenIdMessageJsonConverter<T>>.Instance;
        }

        public OpenIdMessageJsonConverter(ILogger<OpenIdMessageJsonConverter<T>> logger)
        {
            _logger = logger;
        }

        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException();

            var value = new T { Logger = _logger };

            while (reader.Read())
            {
                string propertyName;

                // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
                switch (reader.TokenType)
                {
                    case JsonTokenType.Comment:
                        continue;

                    case JsonTokenType.EndObject:
                        return value;

                    case JsonTokenType.PropertyName:
                        propertyName = reader.GetString() ?? throw new JsonException();
                        break;

                    default:
                        throw new JsonException();
                }

                if (!reader.Read())
                    throw new JsonException();

                // ReSharper disable once SwitchExpressionHandlesSomeKnownEnumValuesWithExceptionInDefault
                var stringValues = reader.TokenType switch
                {
                    JsonTokenType.Null => StringValues.Empty,
                    JsonTokenType.String => new StringValues(reader.GetString()?.Split(OpenIdConstants.ParameterSeparator) ?? Array.Empty<string>()),
                    JsonTokenType.Number => new StringValues(reader.GetDecimal().ToString(CultureInfo.InvariantCulture)),
                    JsonTokenType.True => new StringValues(reader.GetBoolean().ToString(CultureInfo.InvariantCulture)),
                    JsonTokenType.False => new StringValues(reader.GetBoolean().ToString(CultureInfo.InvariantCulture)),
                    _ => throw new JsonException()
                };

                if (!value.TryLoad(propertyName, stringValues, out var result))
                    value.LoadErrors.Add(result);
            }

            throw new JsonException();
        }

        public override void Write(Utf8JsonWriter writer, T? value, JsonSerializerOptions options)
        {
            if (value == null)
            {
                writer.WriteNullValue();
                return;
            }

            writer.WriteStartObject();

            foreach (var parameter in value.Parameters)
            {
                var stringValue = string.Join(OpenIdConstants.ParameterSeparator, parameter.StringValues.AsEnumerable());
                writer.WriteString(parameter.Descriptor.ParameterName, stringValue);
            }

            writer.WriteEndObject();
        }
    }
}
