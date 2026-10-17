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
using JetBrains.Annotations;

namespace NCode.Json;

/// <summary>
/// The family's single date/time coercion policy. The loosely-typed mappers (<see cref="ObjectJsonConverter"/>,
/// <see cref="JsonElements"/>, and <see cref="ObjectGraphExtensions"/>) and any package that turns a date/time string
/// or an RFC 7519 <c>NumericDate</c> into a CLR value parse through it, so they all accept the same formats and produce
/// identical values. String parsing is lenient (invariant culture, any format the corresponding <c>TryParse</c>
/// accepts) and never yields an ambiguous result: a zone-less value is treated as UTC, and an explicit offset is
/// honored. <c>NumericDate</c> conversion is range-guarded: a value outside the representable
/// <see cref="DateTimeOffset"/> range fails instead of throwing, so wire-supplied input can never fault a caller.
/// </summary>
/// <remarks>
/// Lenient parsing accepts far more than ISO 8601 (for example <c>"1.2"</c> or <c>"10:30"</c>, the latter resolved
/// against the current date), so apply it only where the caller has already established that the text is meant to be
/// a date/time — never to sniff whether an arbitrary string "looks like" one.
/// </remarks>
[PublicAPI]
public static class DateCoercion
{
    // Inclusive Unix-second bounds of DateTimeOffset.MinValue/MaxValue; guards NumericDate against overflow.
    private const long MinNumericDateSeconds = -62_135_596_800;
    private const long MaxNumericDateSeconds = 253_402_300_799;

    // A NumericDate's text is a JSON number: an optional sign, digits, and an optional fraction and exponent.
    private const NumberStyles NumericDateStyles =
        NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent;

    /// <summary>
    /// Attempts to convert an RFC 7519 <c>NumericDate</c> of whole <paramref name="seconds"/> since the Unix epoch
    /// (UTC) to a <see cref="DateTimeOffset"/>.
    /// </summary>
    /// <param name="seconds">The number of seconds since <c>1970-01-01T00:00:00Z</c>, ignoring leap seconds.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the UTC instant.</param>
    /// <returns>
    /// <see langword="true"/> if the instant is within the representable <see cref="DateTimeOffset"/> range; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    public static bool TryFromNumericDate(long seconds, out DateTimeOffset value)
    {
        if (seconds is < MinNumericDateSeconds or > MaxNumericDateSeconds)
        {
            value = default;
            return false;
        }

        value = DateTimeOffset.UnixEpoch.AddTicks(seconds * TimeSpan.TicksPerSecond);
        return true;
    }

    /// <summary>
    /// Attempts to convert an RFC 7519 <c>NumericDate</c> of possibly fractional <paramref name="seconds"/> since the
    /// Unix epoch (UTC) to a <see cref="DateTimeOffset"/>, keeping the fraction to tick precision.
    /// </summary>
    /// <param name="seconds">The number of seconds since <c>1970-01-01T00:00:00Z</c>, ignoring leap seconds.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the UTC instant.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="seconds"/> is finite and the instant is within the representable
    /// <see cref="DateTimeOffset"/> range; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryFromNumericDate(double seconds, out DateTimeOffset value)
    {
        var wholeSeconds = Math.Floor(seconds);
        if (
            !double.IsFinite(seconds)
            || wholeSeconds < MinNumericDateSeconds
            || wholeSeconds > MaxNumericDateSeconds
        )
        {
            value = default;
            return false;
        }

        var ticks =
            (long)wholeSeconds * TimeSpan.TicksPerSecond
            + (long)((seconds - wholeSeconds) * TimeSpan.TicksPerSecond);
        value = DateTimeOffset.UnixEpoch.AddTicks(ticks);
        return true;
    }

    /// <summary>
    /// Attempts to parse <paramref name="text"/> as an RFC 7519 <c>NumericDate</c> — the JSON number text of the
    /// seconds since the Unix epoch (UTC), integer or fractional — as carried in a claim value or a request parameter.
    /// Parsing is invariant-culture and strict: no whitespace, thousands separators, or date/time formats.
    /// </summary>
    /// <param name="text">The text to parse; <see langword="null"/> is accepted and fails the parse.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the UTC instant.</param>
    /// <returns>
    /// <see langword="true"/> if <paramref name="text"/> is a number whose instant is within the representable
    /// <see cref="DateTimeOffset"/> range; otherwise, <see langword="false"/>.
    /// </returns>
    public static bool TryParseNumericDate(string? text, out DateTimeOffset value)
    {
        if (
            long.TryParse(
                text,
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out var seconds
            )
        )
        {
            return TryFromNumericDate(seconds, out value);
        }

        if (
            double.TryParse(
                text,
                NumericDateStyles,
                CultureInfo.InvariantCulture,
                out var fractionalSeconds
            )
        )
        {
            return TryFromNumericDate(fractionalSeconds, out value);
        }

        value = default;
        return false;
    }

    // Projects an instant onto T when T is DateTimeOffset or DateTime (or their nullable forms); a DateTime is UTC-kind.
    internal static bool TryConvertInstant<T>(
        DateTimeOffset instant,
        [MaybeNullWhen(false)] out T value
    )
    {
        if (typeof(T) == typeof(DateTimeOffset) || typeof(T) == typeof(DateTimeOffset?))
        {
            value = (T)(object)instant;
            return true;
        }

        if (typeof(T) == typeof(DateTime) || typeof(T) == typeof(DateTime?))
        {
            value = (T)(object)instant.UtcDateTime;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Attempts to parse <paramref name="text"/> as a <see cref="DateTime"/> under the shared lenient policy. The
    /// result kind is normalized so it is never <see cref="DateTimeKind.Unspecified"/>: a <c>Z</c>/UTC value is
    /// <see cref="DateTimeKind.Utc"/>, an explicit offset yields the same instant as <see cref="DateTimeKind.Local"/>,
    /// and a zone-less value is labeled <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    /// <param name="text">The text to parse; <see langword="null"/> is accepted and fails the parse.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the parsed value.</param>
    /// <returns><see langword="true"/> if <paramref name="text"/> was parsed; otherwise, <see langword="false"/>.</returns>
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

    /// <summary>
    /// Attempts to parse <paramref name="text"/> as a <see cref="DateTimeOffset"/> under the shared lenient policy. A
    /// zone-less value is treated as UTC (offset zero); an explicit offset is preserved. A value whose instant falls
    /// outside the representable <see cref="DateTimeOffset"/> range fails the parse rather than being clamped.
    /// </summary>
    /// <param name="text">The text to parse; <see langword="null"/> is accepted and fails the parse.</param>
    /// <param name="value">When this method returns <see langword="true"/>, contains the parsed value.</param>
    /// <returns><see langword="true"/> if <paramref name="text"/> was parsed; otherwise, <see langword="false"/>.</returns>
    public static bool TryParseDateTimeOffset(string? text, out DateTimeOffset value) =>
        // AssumeUniversal makes a zone-less value deterministically UTC; an explicit offset is preserved.
        DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out value
        );
}
