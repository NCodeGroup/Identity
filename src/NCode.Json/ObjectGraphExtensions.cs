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

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using JetBrains.Annotations;

namespace NCode.Json;

/// <summary>
/// Provides extension methods for reading typed values out of a loosely-typed object graph — the kind produced by
/// <see cref="ObjectJsonConverter"/>, where JSON objects become <see cref="Dictionary{TKey,TValue}"/> of
/// <see cref="string"/> to <see cref="object"/> and JSON numbers box as the narrowest of
/// <see cref="int"/>/<see cref="long"/>/<see cref="ulong"/>/<see cref="BigInteger"/>.
/// </summary>
[PublicAPI]
public static class ObjectGraphExtensions
{
    private static readonly BigInteger DecimalMin = new(decimal.MinValue);
    private static readonly BigInteger DecimalMax = new(decimal.MaxValue);

    // The receiver is the common base both IDictionary<,> and IReadOnlyDictionary<,> derive from (which are otherwise
    // unrelated), so a single overload serves both — plus any concrete dictionary that implements both — without an
    // ambiguous call. Dictionary receivers are fast-pathed to an O(1) lookup; anything else is scanned.
    extension(IEnumerable<KeyValuePair<string, object>> collection)
    {
        /// <summary>
        /// Attempts to retrieve a value from the object graph and convert it to <typeparamref name="T"/>.
        /// </summary>
        /// <param name="key">The key of the value to retrieve.</param>
        /// <param name="value">When this method returns, the value converted to <typeparamref name="T"/> if the key was
        /// found and convertible; otherwise, the default value for <typeparamref name="T"/>.</param>
        /// <typeparam name="T">The target type.</typeparam>
        /// <returns><c>true</c> if a value with the specified key was found and converted; otherwise, <c>false</c>.</returns>
        /// <remarks>
        /// Beyond an exact runtime-type match, a boxed numeric value is converted across the integer widths
        /// (<see cref="sbyte"/>…<see cref="ulong"/>, <see cref="BigInteger"/>) and real types
        /// (<see cref="float"/>/<see cref="double"/>/<see cref="decimal"/>) with range checks — so a value does not have
        /// to be read back as the exact CLR width it happened to box as. A boxed integer requested as
        /// <see cref="DateTimeOffset"/> or <see cref="DateTime"/> is interpreted as an RFC 7519 <c>NumericDate</c>
        /// (Unix seconds, UTC).
        /// </remarks>
        public bool TryGetValue<T>(string key, [MaybeNullWhen(false)] out T value)
        {
            if (TryGetObject(collection, key, out var obj))
            {
                return TryConvert(obj, out value);
            }

            value = default;
            return false;
        }
    }

    private static bool TryGetObject(
        IEnumerable<KeyValuePair<string, object>> collection,
        string key,
        out object? obj
    )
    {
        switch (collection)
        {
            case IReadOnlyDictionary<string, object> readOnlyDictionary:
                return readOnlyDictionary.TryGetValue(key, out obj);

            case IDictionary<string, object> dictionary:
                return dictionary.TryGetValue(key, out obj);

            default:
                foreach (var pair in collection)
                {
                    if (string.Equals(pair.Key, key, StringComparison.Ordinal))
                    {
                        obj = pair.Value;
                        return true;
                    }
                }

                obj = null;
                return false;
        }
    }

    private static bool TryConvert<T>(object? obj, [MaybeNullWhen(false)] out T value)
    {
        // Exact runtime-type match (reference types, strings, bools, Guids, dates, and matching numeric widths).
        if (obj is T typed)
        {
            value = typed;
            return true;
        }

        // Each boxed integer widens implicitly to BigInteger, so a single type dispatch feeds the conversion.
        switch (obj)
        {
            case sbyte v:
                return TryConvertFromInteger(v, out value);
            case byte v:
                return TryConvertFromInteger(v, out value);
            case short v:
                return TryConvertFromInteger(v, out value);
            case ushort v:
                return TryConvertFromInteger(v, out value);
            case int v:
                return TryConvertFromInteger(v, out value);
            case uint v:
                return TryConvertFromInteger(v, out value);
            case long v:
                return TryConvertFromInteger(v, out value);
            case ulong v:
                return TryConvertFromInteger(v, out value);
            case BigInteger v:
                return TryConvertFromInteger(v, out value);
            case float or double or decimal:
                return TryConvertFromReal(obj, out value);
            default:
                value = default;
                return false;
        }
    }

    private static bool TryConvertFromInteger<T>(
        BigInteger source,
        [MaybeNullWhen(false)] out T value
    )
    {
        if (IsType<T, sbyte>() && source >= sbyte.MinValue && source <= sbyte.MaxValue)
            return Boxed((sbyte)source, out value);
        if (IsType<T, byte>() && source >= byte.MinValue && source <= byte.MaxValue)
            return Boxed((byte)source, out value);
        if (IsType<T, short>() && source >= short.MinValue && source <= short.MaxValue)
            return Boxed((short)source, out value);
        if (IsType<T, ushort>() && source >= ushort.MinValue && source <= ushort.MaxValue)
            return Boxed((ushort)source, out value);
        if (IsType<T, int>() && source >= int.MinValue && source <= int.MaxValue)
            return Boxed((int)source, out value);
        if (IsType<T, uint>() && source >= uint.MinValue && source <= uint.MaxValue)
            return Boxed((uint)source, out value);
        if (IsType<T, long>() && source >= long.MinValue && source <= long.MaxValue)
            return Boxed((long)source, out value);
        if (IsType<T, ulong>() && source >= ulong.MinValue && source <= ulong.MaxValue)
            return Boxed((ulong)source, out value);
        if (IsType<T, BigInteger>())
            return Boxed(source, out value);

        if (IsType<T, float>())
            return Boxed((float)source, out value);
        if (IsType<T, double>())
            return Boxed((double)source, out value);
        if (IsType<T, decimal>() && source >= DecimalMin && source <= DecimalMax)
            return Boxed((decimal)source, out value);

        // RFC 7519 NumericDate (integer seconds since the Unix epoch, UTC); skipped unless a date is requested.
        if (
            (IsType<T, DateTimeOffset>() || IsType<T, DateTime>())
            && source >= DateCoercion.MinNumericDateSeconds
            && source <= DateCoercion.MaxNumericDateSeconds
        )
            return DateCoercion.TryFromNumericDate((long)source, out value);

        value = default;
        return false;
    }

    private static bool TryConvertFromReal<T>(object source, [MaybeNullWhen(false)] out T value)
    {
        if (IsType<T, float>())
            return Boxed(Convert.ToSingle(source, CultureInfo.InvariantCulture), out value);
        if (IsType<T, double>())
            return Boxed(Convert.ToDouble(source, CultureInfo.InvariantCulture), out value);

        if (IsType<T, decimal>())
        {
            try
            {
                return Boxed(Convert.ToDecimal(source, CultureInfo.InvariantCulture), out value);
            }
            catch (OverflowException)
            {
                value = default;
                return false;
            }
        }

        value = default;
        return false;
    }

    private static bool IsType<T, TTarget>()
        where TTarget : struct => typeof(T) == typeof(TTarget) || typeof(T) == typeof(TTarget?);

    private static bool Boxed<T, TValue>(TValue source, out T value)
        where TValue : struct
    {
        value = (T)(object)source;
        return true;
    }
}
