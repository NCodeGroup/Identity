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

using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Primitives;
using NIdentity.OpenId.Messages.Parameters;
using NIdentity.OpenId.Servers;

namespace NIdentity.OpenId.Messages.Parsers;

/// <summary>
/// Provides a default implementation of <see cref="IJsonParser"/> that parses JSON parameters.
/// </summary>
public class DefaultJsonParser : IJsonParser
{
    /// <summary>
    /// Gets a singleton instance for the <see cref="DefaultJsonParser"/> class.
    /// </summary>
    public static DefaultJsonParser Singleton { get; } = new();

    /// <inheritdoc/>
    public Parameter Read(
        ref Utf8JsonReader reader,
        OpenIdServer openIdServer,
        ParameterDescriptor descriptor,
        JsonSerializerOptions options)
    {
        StringValues stringValues;
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                stringValues = StringValues.Empty;
                break;

            case JsonTokenType.String:
                stringValues = reader.GetString()?.Split(OpenIdConstants.ParameterSeparatorChar);
                break;

            case JsonTokenType.Number:
                stringValues = reader.GetDouble().ToString(CultureInfo.InvariantCulture);
                break;

            case JsonTokenType.True:
            case JsonTokenType.False:
                stringValues = reader.GetBoolean().ToString(CultureInfo.InvariantCulture);
                break;

            default:
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(ref reader, options);
                return descriptor.Loader.Load(openIdServer, descriptor, jsonElement.GetRawText(), jsonElement);
        }

        return descriptor.Loader.Load(openIdServer, descriptor, stringValues);
    }

    /// <inheritdoc/>
    public void Write(
        Utf8JsonWriter writer,
        OpenIdServer openIdServer,
        Parameter parameter,
        JsonSerializerOptions options)
    {
        var stringValue = string.Join(OpenIdConstants.ParameterSeparatorString, parameter.StringValues.AsEnumerable());
        writer.WriteString(parameter.Descriptor.ParameterName, stringValue);
    }
}
