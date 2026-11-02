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

        var parsedValue = reader.GetString();
        return string.IsNullOrEmpty(parsedValue)
            ? StringValues.Empty
            : parsedValue.Split(OpenIdProtocol.ParameterSeparatorChar);
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
