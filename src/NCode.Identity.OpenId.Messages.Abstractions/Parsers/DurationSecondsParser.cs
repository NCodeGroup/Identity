#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using JetBrains.Annotations;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Messages.Parameters;

namespace NCode.Identity.OpenId.Messages.Parsers;

/// <summary>
/// Parses a nullable <see cref="TimeSpan"/> carried as an integer number of seconds. Unlike the generic value parsers
/// (which emit every value as a JSON string under the OpenID wire format), a seconds-valued duration such as
/// <c>expires_in</c> (OAuth 2.0 §5.1) or <c>interval</c> must be a JSON <strong>number</strong>, so this parser writes
/// and reads it as one.
/// </summary>
[PublicAPI]
public class DurationSecondsParser : ParameterParser<TimeSpan?>
{
    /// <inheritdoc/>
    public override StringValues GetStringValues(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        TimeSpan? parsedValue
    ) =>
        parsedValue.HasValue
            ? ((long)parsedValue.Value.TotalSeconds).ToString(CultureInfo.InvariantCulture)
            : StringValues.Empty;

    /// <inheritdoc/>
    public override TimeSpan? Parse(
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        StringValues stringValues
    )
    {
        switch (stringValues.Count)
        {
            case 0 when descriptor.AllowMissingStringValues:
                return null;

            case 0:
                throw openIdEnvironment
                    .ErrorFactory.MissingParameter(descriptor.ParameterName)
                    .AsException();

            case > 1:
                throw openIdEnvironment
                    .ErrorFactory.TooManyParameterValues(descriptor.ParameterName)
                    .AsException();
        }

        if (!long.TryParse(stringValues[0], CultureInfo.InvariantCulture, out var seconds))
        {
            throw openIdEnvironment
                .ErrorFactory.InvalidParameterValue(descriptor.ParameterName)
                .AsException();
        }

        return TimeSpan.FromSeconds(seconds);
    }

    /// <inheritdoc/>
    public override IParameter Read(
        ref Utf8JsonReader reader,
        OpenIdEnvironment openIdEnvironment,
        ParameterDescriptor descriptor,
        SerializationFormat format,
        JsonSerializerOptions options
    )
    {
        TimeSpan? parsedValue = reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => TimeSpan.FromSeconds(reader.GetInt64()),
            JsonTokenType.String => Parse(
                openIdEnvironment,
                descriptor,
                Deserialize<StringValues>(ref reader, options)
            ),
            _ => throw new JsonException("Expected a number of seconds."),
        };
        return Create(openIdEnvironment, descriptor, this, parsedValue);
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        OpenIdEnvironment openIdEnvironment,
        IParameter parameter,
        SerializationFormat format,
        JsonSerializerOptions options
    )
    {
        writer.WritePropertyName(parameter.Descriptor.ParameterName);

        var parsedValue = ((IParameter<TimeSpan?>)parameter).ParsedValue;
        if (parsedValue.HasValue)
        {
            writer.WriteNumberValue((long)parsedValue.Value.TotalSeconds);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}
