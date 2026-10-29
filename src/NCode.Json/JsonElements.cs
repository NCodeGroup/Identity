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
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using JetBrains.Annotations;
using NCode.Buffers;

namespace NCode.Json;

/// <summary>
/// Provides shared, reusable <see cref="JsonElement"/> values and factories. These avoid the common pitfalls of
/// hand-rolling an "empty object" element: forgetting to <see cref="JsonElement.Clone"/> a value out of a
/// <see cref="JsonDocument"/> before it is disposed (which yields an <see cref="ObjectDisposedException"/> on later
/// access), and accidentally using a <see langword="default"/> element whose <see cref="JsonElement.ValueKind"/> is
/// <see cref="JsonValueKind.Undefined"/> rather than a well-formed object.
/// </summary>
[PublicAPI]
public static class JsonElements
{
    private static readonly JsonElement EmptyObjectValue = JsonElement.Parse("{}");

    /// <summary>
    /// Gets a cached, immutable, detached <see cref="JsonElement"/> representing an empty JSON object (<c>{}</c>). The
    /// value is safe to share and re-use because it is cloned free of its owning <see cref="JsonDocument"/>.
    /// </summary>
    public static JsonElement EmptyObject => EmptyObjectValue;

    extension(JsonElement jsonElement)
    {
        /// <summary>
        /// Parses one JSON value from UTF-8 text held in a sequence of segments, such as a buffered HTTP response body,
        /// into a detached <see cref="JsonElement"/> with nothing to dispose, as <see cref="JsonElement.Parse(string, JsonDocumentOptions)"/>
        /// does for contiguous text. It honors every <see cref="JsonDocumentOptions"/> member, as
        /// <see cref="JsonElement.Parse(ReadOnlySpan{byte}, JsonDocumentOptions)"/> does. The text must hold exactly one
        /// value (RFC 8259 §2): anything after it but whitespace is malformed.
        /// </summary>
        /// <param name="utf8Json">The UTF-8 JSON text.</param>
        /// <param name="options">The options that control the reader during parsing.</param>
        /// <returns>The parsed value.</returns>
        /// <exception cref="JsonException">The text is not one well-formed JSON value.</exception>
        public static JsonElement Parse(
            ReadOnlySequence<byte> utf8Json,
            JsonDocumentOptions options = default
        )
        {
            if (utf8Json.IsSingleSegment)
            {
                return JsonElement.Parse(utf8Json.FirstSpan, options);
            }

            // A response body may carry tokens, so the contiguous copy is sensitive.
            using var lease = BufferFactory.Rent(
                checked((int)utf8Json.Length),
                isSensitive: true,
                out Span<byte> buffer
            );
            utf8Json.CopyTo(buffer);

            return JsonElement.Parse(buffer, options);
        }

        /// <summary>
        /// Gets a value indicating whether this element is absent — its <see cref="JsonElement.ValueKind"/> is
        /// <see cref="JsonValueKind.Null"/> or <see cref="JsonValueKind.Undefined"/> — rather than a present value
        /// (which includes an empty object or array).
        /// </summary>
        public bool IsNullOrUndefined() =>
            jsonElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined;

        /// <summary>
        /// Returns this element when it is present, or the shared <see cref="EmptyObject"/> when it is null or
        /// undefined. Useful at persistence boundaries where an absent JSON bag should round-trip as an empty object
        /// rather than a malformed <c>Undefined</c> element.
        /// </summary>
        public JsonElement OrEmptyObject() =>
            jsonElement.IsNullOrUndefined() ? EmptyObject : jsonElement;

        /// <summary>
        /// Attempts to get the value of a JSON property with the specified name, converting it to <typeparamref name="T"/>.
        /// </summary>
        /// <param name="propertyName">The name of the JSON property to get.</param>
        /// <param name="value">When this method returns, contains the value of the JSON property if it was successfully found and converted to <typeparamref name="T"/>.</param>
        /// <typeparam name="T">The target type the JSON property value is converted to. See <see cref="TryGetValue{T}"/> for the supported types.</typeparam>
        /// <returns><c>true</c> if the operation succeeded; otherwise, <c>false</c> if the JSON property wasn't found or the value wasn't able to be converted to <typeparamref name="T"/>.</returns>
        public bool TryGetPropertyValue<T>(string propertyName, [MaybeNullWhen(false)] out T value)
        {
            if (!jsonElement.TryGetProperty(propertyName, out var property))
            {
                value = default;
                return false;
            }

            return property.TryGetValue(out value);
        }

        /// <summary>
        /// Attempts to read the JSON property with the specified name as an RFC 7519 <c>NumericDate</c>. See
        /// <see cref="TryGetNumericDate"/>.
        /// </summary>
        /// <param name="propertyName">The name of the JSON property to read.</param>
        /// <param name="value">When this method returns <see langword="true"/>, contains the UTC instant.</param>
        /// <returns>
        /// <see langword="true"/> if the property exists and is a valid <c>NumericDate</c>; otherwise,
        /// <see langword="false"/>.
        /// </returns>
        public bool TryGetPropertyNumericDate(string propertyName, out DateTimeOffset value)
        {
            if (!jsonElement.TryGetProperty(propertyName, out var property))
            {
                value = default;
                return false;
            }

            return property.TryGetNumericDate(out value);
        }

        /// <summary>
        /// Attempts to read this JSON value as an RFC 7519 <c>NumericDate</c>: a JSON number (integer or fractional) of
        /// seconds since the Unix epoch, UTC. A JSON string is never a <c>NumericDate</c>, and a value outside the
        /// representable <see cref="DateTimeOffset"/> range fails rather than throwing. Read a JWT time claim
        /// (<c>exp</c>, <c>nbf</c>, <c>iat</c>, <c>auth_time</c>) with this, not with <see cref="TryGetValue{T}"/>,
        /// which also parses date strings.
        /// </summary>
        /// <param name="value">When this method returns <see langword="true"/>, contains the UTC instant.</param>
        /// <returns>
        /// <see langword="true"/> if this value is a valid <c>NumericDate</c>; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetNumericDate(out DateTimeOffset value)
        {
            if (jsonElement.ValueKind == JsonValueKind.Number)
            {
                if (jsonElement.TryGetInt64(out var seconds))
                    return DateCoercion.TryFromNumericDate(seconds, out value);

                if (jsonElement.TryGetDouble(out var fractionalSeconds))
                    return DateCoercion.TryFromNumericDate(fractionalSeconds, out value);
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Attempts to read the JSON property with the specified name as a whole number within the range of an
        /// <see cref="int"/>. See <see cref="TryGetWholeInt32"/>.
        /// </summary>
        /// <param name="propertyName">The name of the JSON property to read.</param>
        /// <param name="value">When this method returns <see langword="true"/>, contains the whole number.</param>
        /// <returns>
        /// <see langword="true"/> if the property exists and is a whole number within the range of an
        /// <see cref="int"/>; otherwise, <see langword="false"/>.
        /// </returns>
        public bool TryGetPropertyWholeInt32(string propertyName, out int value)
        {
            if (!jsonElement.TryGetProperty(propertyName, out var property))
            {
                value = default;
                return false;
            }

            return property.TryGetWholeInt32(out value);
        }

        /// <summary>
        /// Attempts to read this JSON value as a whole number within the range of an <see cref="int"/>. JSON has one
        /// number type, so a whole value written <c>60</c>, <c>60.0</c> or <c>6e1</c> reads as 60. A non-zero fraction
        /// (<c>60.5</c>), a value outside the <see cref="int"/> range, a JSON string and any other value kind return
        /// <see langword="false"/> rather than throwing. Use it for a whole number a remote party sends, such as a
        /// lifetime in seconds.
        /// </summary>
        /// <param name="value">When this method returns <see langword="true"/>, contains the whole number.</param>
        /// <returns>
        /// <see langword="true"/> if this value is a whole number within the range of an <see cref="int"/>; otherwise,
        /// <see langword="false"/>.
        /// </returns>
        public bool TryGetWholeInt32(out int value)
        {
            if (jsonElement.ValueKind == JsonValueKind.Number)
            {
                if (jsonElement.TryGetInt32(out value))
                    return true;

                if (
                    jsonElement.TryGetDecimal(out var number)
                    && decimal.Truncate(number) == number
                    && number >= int.MinValue
                    && number <= int.MaxValue
                )
                {
                    value = (int)number;
                    return true;
                }
            }

            value = default;
            return false;
        }

        /// <summary>
        /// Attempts to convert this JSON value to <typeparamref name="T"/>.
        /// </summary>
        /// <param name="value">When this method returns, contains the converted value if the conversion succeeded.</param>
        /// <typeparam name="T">
        /// The target type the JSON value is converted to. Supported types (and their <see cref="Nullable{T}"/>
        /// counterparts) are: <see cref="JsonElement"/> (returned as-is); <see cref="string"/>, <see cref="bool"/>,
        /// <see cref="Guid"/>, <see cref="DateTime"/>, <see cref="DateTimeOffset"/>, <see cref="byte"/>[]; the
        /// integer types <see cref="sbyte"/>, <see cref="byte"/>, <see cref="short"/>, <see cref="ushort"/>,
        /// <see cref="int"/>, <see cref="uint"/>, <see cref="long"/>, <see cref="ulong"/>; and the real types
        /// <see cref="float"/>, <see cref="double"/>, <see cref="decimal"/>. A JSON number requested as
        /// <see cref="DateTime"/> or <see cref="DateTimeOffset"/> is interpreted as an RFC 7519 <c>NumericDate</c>
        /// (integer or fractional Unix seconds, UTC; see <see cref="TryGetNumericDate"/>). A JSON string requested as a
        /// date/time or <see cref="Guid"/> is parsed leniently (any format the corresponding <c>TryParse</c> accepts,
        /// invariant culture), matching <see cref="ObjectJsonConverter"/>:
        /// a <c>Z</c>/UTC value is <see cref="DateTimeKind.Utc"/>, an explicit offset is <see cref="DateTimeKind.Local"/>,
        /// and a zone-less value is labeled <see cref="DateTimeKind.Utc"/> (never <see cref="DateTimeKind.Unspecified"/>).
        /// A <see cref="byte"/>[] is decoded from either base64 or base64url.
        /// </typeparam>
        /// <returns><c>true</c> if the value was converted to <typeparamref name="T"/>; otherwise, <c>false</c>.</returns>
        public bool TryGetValue<T>([MaybeNullWhen(false)] out T value)
        {
            // JsonElement (or object) is returned as-is, including Null/Undefined kinds.
            if (jsonElement is T self)
            {
                value = self;
                return true;
            }

            switch (jsonElement.ValueKind)
            {
                case JsonValueKind.String:
                    return TryConvertFromString(jsonElement, out value);

                case JsonValueKind.Number:
                    return TryConvertFromNumber(jsonElement, out value);

                case JsonValueKind.True:
                case JsonValueKind.False:
                    if (typeof(T) == typeof(bool) || typeof(T) == typeof(bool?))
                    {
                        value = (T)(object)jsonElement.GetBoolean();
                        return true;
                    }
                    break;
            }

            value = default;
            return false;
        }
    }

    private static bool TryConvertFromString<T>(
        JsonElement element,
        [MaybeNullWhen(false)] out T value
    )
    {
        if (typeof(T) == typeof(string))
        {
            var stringValue = element.GetString();
            if (stringValue is not null)
            {
                value = (T)(object)stringValue;
                return true;
            }
        }
        else if (
            (typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(DateTimeOffset?))
            && TryGetDateTimeOffset(element, out var dateTimeOffsetValue)
        )
        {
            value = (T)(object)dateTimeOffsetValue;
            return true;
        }
        else if (
            (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
            && TryGetDateTime(element, out var dateTimeValue)
        )
        {
            value = (T)(object)dateTimeValue;
            return true;
        }
        else if (
            (typeof(T) == typeof(Guid) || typeof(T) == typeof(Guid?))
            && TryGetGuid(element, out var guidValue)
        )
        {
            value = (T)(object)guidValue;
            return true;
        }
        else if (typeof(T) == typeof(byte[]) && TryGetBytes(element, out var bytesValue))
        {
            value = (T)(object)bytesValue;
            return true;
        }

        value = default;
        return false;
    }

    private static bool TryGetDateTimeOffset(JsonElement element, out DateTimeOffset value) =>
        DateCoercion.TryParseDateTimeOffset(element.GetString(), out value);

    private static bool TryGetDateTime(JsonElement element, out DateTime value) =>
        DateCoercion.TryParseDateTime(element.GetString(), out value);

    private static bool TryGetGuid(JsonElement element, out Guid value) =>
        // Guid.TryParse accepts all standard Guid formats (D/N/B/P/X), not only the hyphenated form the strict
        // JsonElement.TryGetGuid reader allows — matching ObjectJsonConverter.
        Guid.TryParse(element.GetString(), out value);

    private static bool TryGetBytes(JsonElement element, [NotNullWhen(true)] out byte[]? value)
    {
        // Standard base64 (RFC 4648 §4).
        if (element.TryGetBytesFromBase64(out value))
        {
            return true;
        }

        // base64url (RFC 4648 §5), the JOSE/JWT encoding.
        var text = element.GetString();
        if (text is not null && Base64Url.IsValid(text))
        {
            value = Base64Url.DecodeFromChars(text);
            return true;
        }

        value = null;
        return false;
    }

    private static bool TryConvertFromNumber<T>(
        JsonElement element,
        [MaybeNullWhen(false)] out T value
    )
    {
        if (
            (typeof(T) == typeof(sbyte) || typeof(T) == typeof(sbyte?))
            && element.TryGetSByte(out var sbyteValue)
        )
        {
            value = (T)(object)sbyteValue;
            return true;
        }

        if (
            (typeof(T) == typeof(byte) || typeof(T) == typeof(byte?))
            && element.TryGetByte(out var byteValue)
        )
        {
            value = (T)(object)byteValue;
            return true;
        }

        if (
            (typeof(T) == typeof(short) || typeof(T) == typeof(short?))
            && element.TryGetInt16(out var shortValue)
        )
        {
            value = (T)(object)shortValue;
            return true;
        }

        if (
            (typeof(T) == typeof(ushort) || typeof(T) == typeof(ushort?))
            && element.TryGetUInt16(out var ushortValue)
        )
        {
            value = (T)(object)ushortValue;
            return true;
        }

        if (
            (typeof(T) == typeof(int) || typeof(T) == typeof(int?))
            && element.TryGetInt32(out var intValue)
        )
        {
            value = (T)(object)intValue;
            return true;
        }

        if (
            (typeof(T) == typeof(uint) || typeof(T) == typeof(uint?))
            && element.TryGetUInt32(out var uintValue)
        )
        {
            value = (T)(object)uintValue;
            return true;
        }

        if (
            (typeof(T) == typeof(long) || typeof(T) == typeof(long?))
            && element.TryGetInt64(out var longValue)
        )
        {
            value = (T)(object)longValue;
            return true;
        }

        if (
            (typeof(T) == typeof(ulong) || typeof(T) == typeof(ulong?))
            && element.TryGetUInt64(out var ulongValue)
        )
        {
            value = (T)(object)ulongValue;
            return true;
        }

        if (
            (typeof(T) == typeof(float) || typeof(T) == typeof(float?))
            && element.TryGetSingle(out var floatValue)
        )
        {
            value = (T)(object)floatValue;
            return true;
        }

        if (
            (typeof(T) == typeof(double) || typeof(T) == typeof(double?))
            && element.TryGetDouble(out var doubleValue)
        )
        {
            value = (T)(object)doubleValue;
            return true;
        }

        if (
            (typeof(T) == typeof(decimal) || typeof(T) == typeof(decimal?))
            && element.TryGetDecimal(out var decimalValue)
        )
        {
            value = (T)(object)decimalValue;
            return true;
        }

        // RFC 7519 NumericDate: seconds since the Unix epoch (UTC), integer or fractional, range-guarded.
        if (
            (
                typeof(T) == typeof(DateTimeOffset)
                || typeof(T) == typeof(DateTimeOffset?)
                || typeof(T) == typeof(DateTime)
                || typeof(T) == typeof(DateTime?)
            ) && element.TryGetNumericDate(out var instant)
        )
        {
            return DateCoercion.TryConvertInstant(instant, out value);
        }

        value = default;
        return false;
    }
}
