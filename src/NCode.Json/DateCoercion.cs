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

namespace NCode.Json;

/// <summary>
/// Shared date/time coercion used by the loosely-typed mappers (<see cref="ObjectJsonConverter"/>,
/// <see cref="JsonElements"/>, and <see cref="ObjectGraphExtensions"/>) so they all produce identical values. Covers
/// both the string path and the RFC 7519 <c>NumericDate</c> (Unix-seconds) path. String parsing is lenient (invariant
/// culture, any format the corresponding <c>TryParse</c> accepts) and normalizes the result kind: a <c>Z</c>/UTC value
/// stays <see cref="DateTimeKind.Utc"/>, an explicit offset becomes <see cref="DateTimeKind.Local"/>, and a zone-less
/// value is labeled <see cref="DateTimeKind.Utc"/> (never <see cref="DateTimeKind.Unspecified"/>).
/// </summary>
internal static class DateCoercion
{
    // Inclusive Unix-second bounds of DateTimeOffset.MinValue/MaxValue; guards NumericDate against overflow.
    public const long MinNumericDateSeconds = -62_135_596_800;
    public const long MaxNumericDateSeconds = 253_402_300_799;

    /// <summary>
    /// Converts an RFC 7519 <c>NumericDate</c> (<paramref name="epochSeconds"/> since the Unix epoch, UTC) to
    /// <typeparamref name="T"/> when it is <see cref="DateTimeOffset"/> or <see cref="DateTime"/> (or their
    /// <see cref="Nullable{T}"/> forms) and the value is within representable range.
    /// </summary>
    public static bool TryFromNumericDate<T>(long epochSeconds, [MaybeNullWhen(false)] out T value)
    {
        var isDateTimeOffset =
            typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(DateTimeOffset?);
        var isDateTime = typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?);

        if (
            (isDateTimeOffset || isDateTime)
            && epochSeconds is >= MinNumericDateSeconds and <= MaxNumericDateSeconds
        )
        {
            var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(epochSeconds);
            value = isDateTimeOffset
                ? (T)(object)dateTimeOffset
                : (T)(object)dateTimeOffset.UtcDateTime;
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryParseDateTime(string? text, out DateTime value)
    {
        // RoundtripKind preserves Z -> Utc and offset -> Local without a machine-local shift of the UTC value.
        if (
            !DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out value
            )
        )
        {
            return false;
        }

        // A zone-less value parses as Unspecified; label it UTC so downstream code never treats it as local.
        if (value.Kind == DateTimeKind.Unspecified)
        {
            value = DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        return true;
    }

    public static bool TryParseDateTimeOffset(string? text, out DateTimeOffset value) =>
        // AssumeUniversal makes a zone-less value deterministically UTC; an explicit offset is preserved.
        DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out value
        );
}
