// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Primitives;

namespace NCode.Identity.OpenId.Serialization;

/// <summary>
/// Provides a <see cref="JsonConverter"/> implementation that can serialize and deserialize <see cref="StringValues"/>
/// instances to and from JSON.
/// </summary>
internal class StringValuesJsonConverter : JsonConverter<StringValues>
{
    /// <inheritdoc />
    public override StringValues Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    )
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            return StringValues.Empty;
        }

        if (reader.TokenType == JsonTokenType.StartArray)
        {
            return ReadArray(ref reader);
        }

        var parsedValue = reader.GetString();
        return string.IsNullOrEmpty(parsedValue)
            ? StringValues.Empty
            : parsedValue.Split(OpenIdProtocol.ParameterSeparatorChar);
    }

    // A JSON array of strings (the 'aud' claim of RFC 7519) holds each element unchanged, without splitting it.
    private static StringValues ReadArray(ref Utf8JsonReader reader)
    {
        var values = new List<string>();
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndArray)
            {
                return values.Count == 0 ? StringValues.Empty : new StringValues([.. values]);
            }

            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException(
                    "Every element of a JSON array of values must be a string."
                );
            }

            values.Add(reader.GetString() ?? string.Empty);
        }

        throw new JsonException("The JSON array of values is not complete.");
    }

    /// <inheritdoc />
    public override void Write(
        Utf8JsonWriter writer,
        StringValues value,
        JsonSerializerOptions options
    )
    {
        switch (value.Count)
        {
            case 0:
                writer.WriteNullValue();
                break;
            case 1:
                writer.WriteStringValue(value[0]);
                break;
            default:
                writer.WriteStringValue(
                    string.Join(OpenIdProtocol.ParameterSeparatorChar, value.AsEnumerable())
                );
                break;
        }
    }
}
