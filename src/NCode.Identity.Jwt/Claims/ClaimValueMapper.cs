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
using System.Security.Claims;
using System.Text.Json;
using NCode.Json;

namespace NCode.Identity.Jwt.Claims;

/// <summary>
/// Maps a single JWT claim value between its JSON form and its <see cref="Claim"/> form (a string value plus an XML
/// Schema value type). Both directions live together so they stay symmetric: decoding keeps the exact JSON text and
/// records the most specific value type, and encoding emits the JSON value the declared type describes — falling back
/// to the verbatim string when the value is not valid for that type, so encoding never throws or guesses.
/// </summary>
internal static class ClaimValueMapper
{
    // The XML Schema lexical spaces: an optional sign and digits for integers; additionally a fraction/exponent for reals.
    private const NumberStyles IntegerStyles = NumberStyles.Integer;
    private const NumberStyles RealStyles = NumberStyles.Float;

    private static JsonElement JsonNull { get; } = JsonElement.Parse("null");

    /// <summary>
    /// Creates a <see cref="Claim"/> from a JSON value. Strings, numbers, objects, and arrays keep their exact JSON text;
    /// a string is never re-typed by its shape (ADR-0121); a number is typed with the most specific of
    /// <see cref="ClaimValueTypes.Integer32"/>, <see cref="ClaimValueTypes.Integer64"/>,
    /// <see cref="ClaimValueTypes.UInteger64"/>, the unbounded <see cref="ClaimValueTypes.Integer"/>, or
    /// <see cref="ClaimValueTypes.Double"/> for a fraction or exponent.
    /// </summary>
    /// <param name="claimType">The claim type (the JSON member name).</param>
    /// <param name="value">The JSON value of the claim.</param>
    /// <param name="issuer">The issuer of the claim.</param>
    /// <param name="subject">The subject of the claim.</param>
    /// <returns>The newly created <see cref="Claim"/> instance.</returns>
    public static Claim ToClaim(
        string claimType,
        JsonElement value,
        string issuer,
        ClaimsIdentity subject
    )
    {
        var (claimValue, valueType) = value.ValueKind switch
        {
            JsonValueKind.Null => (string.Empty, JsonClaimValueTypes.JsonNull),
            JsonValueKind.Object => (value.GetRawText(), JsonClaimValueTypes.Json),
            JsonValueKind.Array => (value.GetRawText(), JsonClaimValueTypes.JsonArray),
            JsonValueKind.String => (value.ToString(), ClaimValueTypes.String),
            JsonValueKind.Number => (value.GetRawText(), GetNumberValueType(value)),
            JsonValueKind.True => ("true", ClaimValueTypes.Boolean),
            JsonValueKind.False => ("false", ClaimValueTypes.Boolean),
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value.ValueKind,
                "Unsupported JSON value kind."
            ),
        };

        return new Claim(claimType, claimValue, valueType, issuer, issuer, subject);
    }

    /// <summary>
    /// Converts a <see cref="Claim"/> to the native value serialized into a JWT payload, according to its
    /// <see cref="Claim.ValueType"/>. A value outside its declared type's lexical space — or one JSON cannot represent,
    /// such as <c>NaN</c> or an out-of-range date — is emitted as its verbatim string.
    /// </summary>
    /// <param name="claim">The claim to convert.</param>
    /// <returns>The native value to serialize.</returns>
    public static object ToJsonValue(Claim claim)
    {
        var value = claim.Value;
        return claim.ValueType switch
        {
            ClaimValueTypes.Boolean when bool.TryParse(value, out var boolean) => boolean,
            ClaimValueTypes.Integer32
                when int.TryParse(
                    value,
                    IntegerStyles,
                    CultureInfo.InvariantCulture,
                    out var int32
                ) => int32,
            ClaimValueTypes.Integer64
                when long.TryParse(
                    value,
                    IntegerStyles,
                    CultureInfo.InvariantCulture,
                    out var int64
                ) => int64,
            ClaimValueTypes.UInteger32
                when uint.TryParse(
                    value,
                    IntegerStyles,
                    CultureInfo.InvariantCulture,
                    out var uint32
                ) => uint32,
            ClaimValueTypes.UInteger64
                when ulong.TryParse(
                    value,
                    IntegerStyles,
                    CultureInfo.InvariantCulture,
                    out var uint64
                ) => uint64,
            ClaimValueTypes.Integer when TryParseInteger(value, out var integer) => integer,
            ClaimValueTypes.Double when TryParseDouble(value, out var real) => real,
            ClaimValueTypes.DateTime
                when DateCoercion.TryParseDateTimeOffset(value, out var dateTime) => dateTime,
            JsonClaimValueTypes.Json
                when TryParseJson(value, JsonValueKind.Object, out var jsonObject) => jsonObject,
            JsonClaimValueTypes.JsonArray
                when TryParseJson(value, JsonValueKind.Array, out var jsonArray) => jsonArray,
            JsonClaimValueTypes.JsonNull => JsonNull,
            _ => value,
        };
    }

    private static string GetNumberValueType(JsonElement value)
    {
        // Most specific first (first match wins), mirroring ObjectJsonConverter's integer mapping.
        if (value.TryGetInt32(out _))
            return ClaimValueTypes.Integer32;

        if (value.TryGetInt64(out _))
            return ClaimValueTypes.Integer64;

        if (value.TryGetUInt64(out _))
            return ClaimValueTypes.UInteger64;

        var rawText = value.GetRawText();
        return rawText.AsSpan().ContainsAny('.', 'e', 'E')
            ? ClaimValueTypes.Double
            : ClaimValueTypes.Integer;
    }

    private static bool TryParseInteger(string value, [NotNullWhen(true)] out object? integer)
    {
        if (long.TryParse(value, IntegerStyles, CultureInfo.InvariantCulture, out var int64))
        {
            integer = int64;
            return true;
        }

        if (ulong.TryParse(value, IntegerStyles, CultureInfo.InvariantCulture, out var uint64))
        {
            integer = uint64;
            return true;
        }

        // xsd:integer is unbounded; beyond 64 bits the canonical literal is emitted as an exact JSON number.
        if (BigInteger.TryParse(value, IntegerStyles, CultureInfo.InvariantCulture, out var big))
        {
            var literal = big.ToString(CultureInfo.InvariantCulture);
            integer = JsonElement.Parse(literal);
            return true;
        }

        integer = null;
        return false;
    }

    private static bool TryParseDouble(string value, out double real) =>
        // JSON cannot represent NaN or the infinities.
        double.TryParse(value, RealStyles, CultureInfo.InvariantCulture, out real)
        && double.IsFinite(real);

    private static bool TryParseJson(
        string value,
        JsonValueKind expectedKind,
        out JsonElement element
    )
    {
        try
        {
            element = JsonElement.Parse(value);
        }
        catch (JsonException)
        {
            element = default;
            return false;
        }

        return element.ValueKind == expectedKind;
    }
}
