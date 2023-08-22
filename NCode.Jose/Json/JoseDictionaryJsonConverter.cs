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

using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NCode.Jose.Json;

/// <summary>
/// Provides the ability to deserialize JSON into a <see cref="Dictionary{TKey,TValue}"/>
/// where <c>TKey</c> is <see cref="string"/> and <c>TValue</c> is <see cref="object"/>.
/// </summary>
public class JoseDictionaryJsonConverter : JsonConverter<object>
{
    /// <inheritdoc />
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var type = typeToConvert;

        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.True:
                return true;

            case JsonTokenType.False:
                return false;

            case JsonTokenType.Number when TryReadNumber(ref reader, out var value):
                return value;

            case JsonTokenType.String:
                return ReadStringOrDateTimeOffset(ref reader);

            case JsonTokenType.StartObject:
                type = typeof(Dictionary<string, object>);
                break;

            case JsonTokenType.StartArray:
                type = typeof(List<object>);
                break;
        }

        return JsonSerializer.Deserialize(ref reader, type, options);
    }

    private static bool TryReadNumber(ref Utf8JsonReader reader, [MaybeNullWhen(false)] out object baseValue)
    {
        Debug.Assert(reader.TokenType == JsonTokenType.Number);

        if (reader.TryGetInt32(out var value1))
        {
            baseValue = value1;
            return true;
        }

        if (reader.TryGetInt64(out var value2))
        {
            baseValue = value2;
            return true;
        }

        if (reader.TryGetUInt32(out var value3))
        {
            baseValue = value3;
            return true;
        }

        if (reader.TryGetUInt64(out var value4))
        {
            baseValue = value4;
            return true;
        }

        if (reader.TryGetDouble(out var value5))
        {
            baseValue = value5;
            return true;
        }

        if (reader.TryGetDecimal(out var value6))
        {
            baseValue = value6;
            return true;
        }

        baseValue = null;
        return false;
    }

    private static object ReadStringOrDateTimeOffset(ref Utf8JsonReader reader)
    {
        Debug.Assert(reader.TokenType == JsonTokenType.String);

        var stringValue = reader.GetString();

        Debug.Assert(stringValue != null);

        if (DateTimeOffset.TryParse(
                stringValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var dateTimeOffset))
        {
            return dateTimeOffset;
        }

        return stringValue;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
