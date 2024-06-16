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
using JetBrains.Annotations;

namespace NCode.Identity.Jose.Json;

/// <summary>
/// Provides the ability to deserialize JSON into an <see cref="object"/> instance.
/// </summary>
/// <remarks>
/// Type conversions are attempted in the following order:
/// <list type="number">
/// <item><term><see cref="DateTimeOffset"/></term></item>
/// <item><term><see cref="DateTime"/></term></item>
/// <item><term><see cref="Guid"/></term></item>
/// <item><term><see cref="Int32"/></term></item>
/// <item><term><see cref="Byte"/></term></item>
/// <item><term><see cref="Int16"/></term></item>
/// <item><term><see cref="Int64"/></term></item>
/// <item><term><see cref="UInt32"/></term></item>
/// <item><term><see cref="UInt16"/></term></item>
/// <item><term><see cref="UInt64"/></term></item>
/// <item><term><see cref="Double"/></term></item>
/// <item><term><see cref="Decimal"/></term></item>
/// <item><term><see cref="Single"/></term></item>
/// </list>
/// </remarks>
[PublicAPI]
public sealed class JoseObjectJsonConverter : JsonConverter<object>
{
    /// <summary>
    /// Gets a singleton instance for <see cref="JoseObjectJsonConverter"/>.
    /// </summary>
    public static JoseObjectJsonConverter Singleton { get; } = new();

    /// <inheritdoc />
    public override object? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (typeToConvert == typeof(JsonDocument) || typeToConvert == typeof(JsonElement))
            return JsonSerializer.Deserialize(ref reader, typeToConvert, options);

        var type = typeToConvert;
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.True when TryReadBoolean(ref reader, typeToConvert, out var value):
                return value;

            case JsonTokenType.False when TryReadBoolean(ref reader, typeToConvert, out var value):
                return value;

            case JsonTokenType.Number when TryReadNumber(ref reader, typeToConvert, out var value):
                return value;

            case JsonTokenType.String when TryReadString(ref reader, typeToConvert, out var value):
                return value;

            case JsonTokenType.StartObject:
                type = typeof(Dictionary<string, object?>);
                break;

            case JsonTokenType.StartArray:
                type = typeof(List<object?>);
                break;
        }

        return JsonSerializer.Deserialize(ref reader, type, options);
    }

    private static bool TryReadBoolean(ref Utf8JsonReader reader, Type typeToConvert, [MaybeNullWhen(false)] out object baseValue)
    {
        Debug.Assert(reader.TokenType is JsonTokenType.True or JsonTokenType.False);

        var boolValue = reader.GetBoolean();

        if (typeof(bool).IsAssignableTo(typeToConvert))
        {
            baseValue = boolValue;
            return true;
        }

        baseValue = null;
        return false;
    }

    private static bool TryReadNumber(ref Utf8JsonReader reader, Type typeToConvert, [MaybeNullWhen(false)] out object baseValue)
    {
        Debug.Assert(reader.TokenType == JsonTokenType.Number);

        if (typeof(int).IsAssignableTo(typeToConvert) && reader.TryGetInt32(out var intValue))
        {
            baseValue = intValue;
            return true;
        }

        if (typeof(byte).IsAssignableTo(typeToConvert) && reader.TryGetByte(out var byteValue))
        {
            baseValue = byteValue;
            return true;
        }

        if (typeof(short).IsAssignableTo(typeToConvert) && reader.TryGetInt16(out var shortValue))
        {
            baseValue = shortValue;
            return true;
        }

        if (typeof(long).IsAssignableTo(typeToConvert) && reader.TryGetInt64(out var longValue))
        {
            baseValue = longValue;
            return true;
        }

        if (typeof(uint).IsAssignableTo(typeToConvert) && reader.TryGetUInt32(out var uintValue))
        {
            baseValue = uintValue;
            return true;
        }

        if (typeof(ushort).IsAssignableTo(typeToConvert) && reader.TryGetUInt16(out var ushortValue))
        {
            baseValue = ushortValue;
            return true;
        }

        if (typeof(ulong).IsAssignableTo(typeToConvert) && reader.TryGetUInt64(out var ulongValue))
        {
            baseValue = ulongValue;
            return true;
        }

        if (typeof(double).IsAssignableTo(typeToConvert) && reader.TryGetDouble(out var doubleValue))
        {
            baseValue = doubleValue;
            return true;
        }

        if (typeof(decimal).IsAssignableTo(typeToConvert) && reader.TryGetDecimal(out var decimalValue))
        {
            baseValue = decimalValue;
            return true;
        }

        if (typeof(float).IsAssignableTo(typeToConvert) && reader.TryGetSingle(out var floatValue))
        {
            baseValue = floatValue;
            return true;
        }

        baseValue = null;
        return false;
    }

    private static bool TryReadString(ref Utf8JsonReader reader, Type typeToConvert, [MaybeNullWhen(false)] out object baseValue)
    {
        Debug.Assert(reader.TokenType == JsonTokenType.String);

        var stringValue = reader.GetString();

        Debug.Assert(stringValue != null);

        if (typeof(DateTimeOffset).IsAssignableTo(typeToConvert) &&
            DateTimeOffset.TryParse(
                stringValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var dateTimeOffset))
        {
            baseValue = dateTimeOffset;
            return true;
        }

        if (typeof(DateTime).IsAssignableTo(typeToConvert) &&
            DateTime.TryParse(
                stringValue,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var dateTime))
        {
            baseValue = dateTime;
            return true;
        }

        if (typeof(Guid).IsAssignableTo(typeToConvert) &&
            Guid.TryParse(stringValue, out var guid))
        {
            baseValue = guid;
            return true;
        }

        if (typeof(int).IsAssignableTo(typeToConvert) &&
            int.TryParse(
                stringValue,
                NumberStyles.Integer |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var intValue))
        {
            baseValue = intValue;
            return true;
        }

        if (typeof(byte).IsAssignableTo(typeToConvert) &&
            byte.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var byteValue))
        {
            baseValue = byteValue;
            return true;
        }

        if (typeof(short).IsAssignableTo(typeToConvert) &&
            short.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var shortValue))
        {
            baseValue = shortValue;
            return true;
        }

        if (typeof(long).IsAssignableTo(typeToConvert) &&
            long.TryParse(
                stringValue,
                NumberStyles.Integer |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var longValue))
        {
            baseValue = longValue;
            return true;
        }

        if (typeof(uint).IsAssignableTo(typeToConvert) &&
            uint.TryParse(
                stringValue,
                NumberStyles.Integer |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var uintValue))
        {
            baseValue = uintValue;
            return true;
        }

        if (typeof(ushort).IsAssignableTo(typeToConvert) &&
            ushort.TryParse(
                stringValue,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var ushortValue))
        {
            baseValue = ushortValue;
            return true;
        }

        if (typeof(ulong).IsAssignableTo(typeToConvert) &&
            ulong.TryParse(
                stringValue,
                NumberStyles.Integer |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var ulongValue))
        {
            baseValue = ulongValue;
            return true;
        }

        if (typeof(double).IsAssignableTo(typeToConvert) &&
            double.TryParse(
                stringValue,
                NumberStyles.Float |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var doubleValue))
        {
            baseValue = doubleValue;
            return true;
        }

        if (typeof(decimal).IsAssignableTo(typeToConvert) &&
            decimal.TryParse(
                stringValue,
                NumberStyles.Number |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var decimalValue))
        {
            baseValue = decimalValue;
            return true;
        }

        if (typeof(float).IsAssignableTo(typeToConvert) &&
            float.TryParse(
                stringValue,
                NumberStyles.Float |
                NumberStyles.AllowThousands,
                CultureInfo.InvariantCulture,
                out var floatValue))
        {
            baseValue = floatValue;
            return true;
        }

        if (typeof(string).IsAssignableTo(typeToConvert))
        {
            baseValue = stringValue;
            return true;
        }

        baseValue = null;
        return false;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options) =>
        JsonSerializer.Serialize(writer, value, value.GetType(), options);
}
