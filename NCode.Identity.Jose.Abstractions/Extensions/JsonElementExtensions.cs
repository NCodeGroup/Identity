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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace NCode.Identity.Jose.Extensions;

/// <summary>
/// Provides extension methods for <see cref="JsonElement"/>.
/// </summary>
public static class JsonElementExtensions
{
    /// <summary>
    /// Attempts to get the value of a JSON property with the specified name.
    /// </summary>
    /// <param name="jsonElement">An <see cref="JsonElement"/> instance.</param>
    /// <param name="propertyName">The name of the JSON property to get.</param>
    /// <param name="value">When this method returns, contains the value of the JSON property if it was successfully found and converted to <typeparamref name="T"/>.</param>
    /// <typeparam name="T">The type of the JSON property to get.</typeparam>
    /// <returns><c>true</c> if the operation succeeded; otherwise, <c>false</c> if the JSON property wasn't found or the value wasn't able to be converted to <typeparamref name="T"/>.</returns>
    public static bool TryGetPropertyValue<T>(this JsonElement jsonElement, string propertyName, [MaybeNullWhen(false)] out T value)
    {
        if (!jsonElement.TryGetProperty(propertyName, out var property))
        {
            value = default;
            return false;
        }

        if (property is T returnValue)
        {
            value = returnValue;
            return true;
        }

        if (property.ValueKind == JsonValueKind.Null)
        {
            value = default;
            return false;
        }

        if (typeof(T) == typeof(string))
        {
            value = (T)(object)property.ToString();
            return true;
        }

        var isBoolType = typeof(T) == typeof(bool) || typeof(T) == typeof(bool?);
        if (isBoolType && property.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            value = (T)(object)property.GetBoolean();
            return true;
        }

        var isDateTimeOffsetType = typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(DateTimeOffset?);
        var isDateTimeType = typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?);
        if (property.ValueKind == JsonValueKind.String && (isDateTimeOffsetType || isDateTimeType))
        {
            var stringValue = property.ToString();

            if (isDateTimeOffsetType &&
                DateTimeOffset.TryParse(
                    stringValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var dateTimeOffsetValue))
            {
                value = (T)(object)dateTimeOffsetValue;
                return true;
            }

            if (isDateTimeType &&
                DateTime.TryParse(
                    stringValue,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal,
                    out var dateTimeValue))
            {
                value = (T)(object)dateTimeValue;
                return true;
            }
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            var isFloatType = typeof(T) == typeof(float) || typeof(T) == typeof(float?);
            if (isFloatType && property.TryGetSingle(out var floatValue))
            {
                value = (T)(object)floatValue;
                return true;
            }

            var isDoubleType = typeof(T) == typeof(double) || typeof(T) == typeof(double?);
            if (isDoubleType && property.TryGetDouble(out var doubleValue))
            {
                value = (T)(object)doubleValue;
                return true;
            }

            var isByteType = typeof(T) == typeof(byte) || typeof(T) == typeof(byte?);
            if (isByteType && property.TryGetByte(out var byteValue))
            {
                value = (T)(object)byteValue;
                return true;
            }

            var isShortType = typeof(T) == typeof(short) || typeof(T) == typeof(short?);
            if (isShortType && property.TryGetInt16(out var shortValue))
            {
                value = (T)(object)shortValue;
                return true;
            }

            var isIntType = typeof(T) == typeof(int) || typeof(T) == typeof(int?);
            if (isIntType && property.TryGetInt32(out var intValue))
            {
                value = (T)(object)intValue;
                return true;
            }

            var isLongType = typeof(T) == typeof(long) || typeof(T) == typeof(long?);
            var isLongValue = property.TryGetInt64(out var longValue);
            if (isLongType && isLongValue)
            {
                value = (T)(object)longValue;
                return true;
            }

            /*

            https://datatracker.ietf.org/doc/html/rfc7519#section-2

            NumericDate
                A JSON numeric value representing the number of seconds from
                1970-01-01T00:00:00Z UTC until the specified UTC date/time,
                ignoring leap seconds.  This is equivalent to the IEEE Std 1003.1,
                2013 Edition [POSIX.1] definition "Seconds Since the Epoch", in
                which each day is accounted for by exactly 86400 seconds, other
                than that non-integer values can be represented.  See RFC 3339
                [RFC3339] for details regarding date/times in general and UTC in
                particular.

            */

            if (isDateTimeOffsetType && isLongValue)
            {
                value = (T)(object)DateTimeOffset.FromUnixTimeSeconds(longValue);
                return true;
            }

            if (isDateTimeType && isLongValue)
            {
                value = (T)(object)DateTimeOffset.FromUnixTimeSeconds(longValue).DateTime;
                return true;
            }
        }

        value = default;
        return false;
    }
}
