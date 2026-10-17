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

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Json;

/// <summary>
/// Provides the ability to deserialize JSON into a loosely-typed <see cref="object"/> graph — JSON objects become
/// <see cref="Dictionary{TKey,TValue}"/> of <see cref="string"/> to <see cref="object"/>, JSON arrays become
/// <see cref="List{T}"/> of <see cref="object"/>, and scalar values are mapped to a CLR type according to the
/// configured <see cref="ObjectJsonConverterOptions"/>.
/// </summary>
/// <remarks>
/// Scalar mapping, with the defaults from <see cref="ObjectJsonConverterOptions.Default"/>:
/// <list type="bullet">
/// <item><description><c>null</c> → <see langword="null"/>; <c>true</c>/<c>false</c> → <see cref="bool"/>.</description></item>
/// <item><description>Integer numbers → the narrowest of <see cref="int"/>, <see cref="long"/>, <see cref="ulong"/>,
/// then <see cref="BigInteger"/> (see <see cref="ObjectJsonConverterOptions.Integer"/> /
/// <see cref="ObjectJsonConverterOptions.AllowBigInteger"/>).</description></item>
/// <item><description>Real numbers → <see cref="double"/> (see <see cref="ObjectJsonConverterOptions.Real"/>).</description></item>
/// <item><description>Strings are coerced to the most specific matching CLR type — <see cref="DateTimeOffset"/>,
/// <see cref="Guid"/>, or a numeric type — falling back to <see cref="string"/> (see
/// <see cref="ObjectJsonConverterOptions.DateString"/>, <see cref="ObjectJsonConverterOptions.CoerceStringToGuid"/>,
/// and <see cref="ObjectJsonConverterOptions.CoerceStringToNumber"/>). Each coercion can be disabled for strict
/// round-trip fidelity.</description></item>
/// </list>
/// <para>
/// Because the target type is unknown when deserializing to <see cref="object"/>, a bare JSON number is inherently
/// ambiguous and is therefore never interpreted as an RFC 7519 <c>NumericDate</c> (Unix seconds). That interpretation
/// only applies where the caller supplies an explicit date target type — see
/// <see cref="JsonElements"/>.<c>TryGetValue&lt;T&gt;</c>.
/// </para>
/// </remarks>
/// <param name="options">The options that control how JSON scalar values are mapped to CLR types.</param>
[PublicAPI]
public sealed class ObjectJsonConverter(ObjectJsonConverterOptions options) : JsonConverter<object>
{
    /// <summary>
    /// Gets a shared instance of <see cref="ObjectJsonConverter"/> configured with
    /// <see cref="ObjectJsonConverterOptions.Default"/>.
    /// </summary>
    public static ObjectJsonConverter Default { get; } = new(ObjectJsonConverterOptions.Default);

    private ObjectJsonConverterOptions Options { get; } = options;

    /// <inheritdoc />
    public override object? Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options
    ) =>
        reader.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.True or JsonTokenType.False => reader.GetBoolean(),
            JsonTokenType.Number => ReadNumber(ref reader),
            JsonTokenType.String => ReadString(ref reader),
            // Deserializing to the concrete container types (rather than object) routes through the built-in
            // dictionary/list converters, so this converter never re-enters itself with an unadvanced reader.
            JsonTokenType.StartObject => JsonSerializer.Deserialize<Dictionary<string, object?>>(
                ref reader,
                options
            ),
            JsonTokenType.StartArray => JsonSerializer.Deserialize<List<object?>>(
                ref reader,
                options
            ),
            _ => throw new JsonException(
                $"Unexpected JSON token '{reader.TokenType}' when reading an object value."
            ),
        };

    private object ReadNumber(ref Utf8JsonReader reader)
    {
        if (
            Options.Integer == ObjectJsonIntegerHandling.MostSpecific
            && reader.TryGetInt32(out var intValue)
        )
        {
            return intValue;
        }

        if (reader.TryGetInt64(out var longValue))
        {
            return longValue;
        }

        if (reader.TryGetUInt64(out var ulongValue))
        {
            return ulongValue;
        }

        if (Options.AllowBigInteger && TryGetBigInteger(ref reader, out var bigValue))
        {
            return bigValue;
        }

        return ReadReal(ref reader);
    }

    private object ReadReal(ref Utf8JsonReader reader)
    {
        if (
            Options.Real == ObjectJsonRealHandling.Decimal
            && reader.TryGetDecimal(out var decValue)
        )
        {
            return decValue;
        }

        if (reader.TryGetDouble(out var doubleValue))
        {
            return doubleValue;
        }

        if (reader.TryGetDecimal(out var decFallback))
        {
            return decFallback;
        }

        throw new JsonException("Unable to read the JSON number as a CLR numeric type.");
    }

    private static bool TryGetBigInteger(ref Utf8JsonReader reader, out BigInteger value)
    {
        ReadOnlySpan<byte> utf8 = reader.HasValueSequence
            ? reader.ValueSequence.ToArray()
            : reader.ValueSpan;

        // A real number (fraction or exponent) is not a BigInteger.
        foreach (var b in utf8)
        {
            if (b is (byte)'.' or (byte)'e' or (byte)'E')
            {
                value = default;
                return false;
            }
        }

        var text = Encoding.UTF8.GetString(utf8);
        return BigInteger.TryParse(
            text,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out value
        );
    }

    private object ReadString(ref Utf8JsonReader reader)
    {
        var value =
            reader.GetString()
            ?? throw new JsonException("Unexpected null value for a JSON string token.");

        switch (Options.DateString)
        {
            case ObjectJsonDateStringHandling.DateTimeOffset
                when DateCoercion.TryParseDateTimeOffset(value, out var dateTimeOffset):
                return dateTimeOffset;

            case ObjectJsonDateStringHandling.DateTime
                when DateCoercion.TryParseDateTime(value, out var dateTime):
                return dateTime;
        }

        if (Options.CoerceStringToGuid && Guid.TryParse(value, out var guid))
        {
            return guid;
        }

        if (Options.CoerceStringToNumber && TryCoerceStringToNumber(value, out var number))
        {
            return number;
        }

        return value;
    }

    private bool TryCoerceStringToNumber(string value, [MaybeNullWhen(false)] out object number)
    {
        const NumberStyles integerStyles = NumberStyles.Integer | NumberStyles.AllowThousands;
        const NumberStyles realStyles = NumberStyles.Float | NumberStyles.AllowThousands;
        const NumberStyles decimalStyles = NumberStyles.Number | NumberStyles.AllowThousands;
        var culture = CultureInfo.InvariantCulture;

        if (
            Options.Integer == ObjectJsonIntegerHandling.MostSpecific
            && int.TryParse(value, integerStyles, culture, out var intValue)
        )
        {
            number = intValue;
            return true;
        }

        if (long.TryParse(value, integerStyles, culture, out var longValue))
        {
            number = longValue;
            return true;
        }

        if (ulong.TryParse(value, integerStyles, culture, out var ulongValue))
        {
            number = ulongValue;
            return true;
        }

        if (
            Options.AllowBigInteger
            && BigInteger.TryParse(value, NumberStyles.Integer, culture, out var bigValue)
        )
        {
            number = bigValue;
            return true;
        }

        if (
            Options.Real == ObjectJsonRealHandling.Decimal
            && decimal.TryParse(value, decimalStyles, culture, out var decValue)
        )
        {
            number = decValue;
            return true;
        }

        if (double.TryParse(value, realStyles, culture, out var doubleValue))
        {
            number = doubleValue;
            return true;
        }

        if (decimal.TryParse(value, decimalStyles, culture, out var decFallback))
        {
            number = decFallback;
            return true;
        }

        number = null;
        return false;
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, object value, JsonSerializerOptions options)
    {
        var type = value.GetType();

        // A bare System.Object has no members; serializing it by its own type would re-enter this converter
        // indefinitely, so emit an empty object — matching the built-in and Newtonsoft.Json behavior.
        if (type == typeof(object))
        {
            writer.WriteStartObject();
            writer.WriteEndObject();
            return;
        }

        // Serializing by the concrete runtime type dispatches to that type's converter (built-in or custom).
        JsonSerializer.Serialize(writer, value, type, options);
    }
}
