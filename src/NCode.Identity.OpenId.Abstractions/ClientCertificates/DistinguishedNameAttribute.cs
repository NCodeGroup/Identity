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

using System.Formats.Asn1;
using System.Text;

namespace NCode.Identity.OpenId.ClientCertificates;

/// <summary>
/// Represents one attribute type and value of a relative distinguished name, reduced to the form in which two
/// attributes are compared: a character-string value is held as prepared text, and any other value is held as its
/// exact encoding.
/// </summary>
/// <param name="Oid">The dotted object identifier of the attribute type.</param>
/// <param name="PreparedText">
/// The prepared text of a character-string value, or <see langword="null"/> when the value is not a character string.
/// </param>
/// <param name="EncodedValue">
/// The exact BER encoding of a value that is not a character string; empty when <paramref name="PreparedText"/> is set.
/// </param>
internal readonly record struct DistinguishedNameAttribute(
    string Oid,
    string? PreparedText,
    ReadOnlyMemory<byte> EncodedValue
)
{
    /// <summary>
    /// Creates an attribute whose value is a character string.
    /// </summary>
    /// <param name="oid">The dotted object identifier of the attribute type.</param>
    /// <param name="text">The character-string value, before preparation.</param>
    /// <exception cref="ArgumentException">The <paramref name="text"/> is not valid Unicode.</exception>
    public static DistinguishedNameAttribute FromText(string oid, string text) =>
        new(oid, Prepare(text), default);

    /// <summary>
    /// Creates an attribute from one BER-encoded value, such as the value of a <c>#</c>-hex attribute or of an
    /// attribute in a certificate: a character-string value is held as prepared text, and any other value is held as
    /// its exact encoding.
    /// </summary>
    /// <param name="oid">The dotted object identifier of the attribute type.</param>
    /// <param name="encodedValue">The BER encoding of exactly one value.</param>
    /// <exception cref="AsnContentException">The <paramref name="encodedValue"/> is not exactly one valid value.</exception>
    public static DistinguishedNameAttribute FromEncodedValue(
        string oid,
        ReadOnlyMemory<byte> encodedValue
    )
    {
        var reader = new AsnReader(encodedValue, AsnEncodingRules.BER);
        var tag = reader.PeekTag();

        if (
            tag.TagClass == TagClass.Universal
            && IsCharacterString((UniversalTagNumber)tag.TagValue)
        )
        {
            var text = reader.ReadCharacterString((UniversalTagNumber)tag.TagValue);
            reader.ThrowIfNotEmpty();
            return FromText(oid, text);
        }

        reader.ReadEncodedValue();
        reader.ThrowIfNotEmpty();
        return new DistinguishedNameAttribute(oid, null, encodedValue);
    }

    /// <summary>
    /// Determines whether this attribute is equal to another one: the types are the same, and the values are either
    /// the same prepared text or, for values that are not character strings, the same encoding.
    /// </summary>
    /// <param name="other">The attribute to compare with.</param>
    public bool Matches(DistinguishedNameAttribute other)
    {
        if (!string.Equals(Oid, other.Oid, StringComparison.Ordinal))
            return false;

        if (PreparedText is not null)
            return string.Equals(PreparedText, other.PreparedText, StringComparison.Ordinal);

        return other.PreparedText is null
            && EncodedValue.Span.SequenceEqual(other.EncodedValue.Span);
    }

    /// <summary>
    /// Prepares a character-string value for comparison: the text is normalized, characters that are mapped to nothing
    /// are deleted, every run of whitespace becomes one space, leading and trailing whitespace is removed, and the
    /// result is upper-cased with the invariant culture.
    /// </summary>
    /// <param name="text">The character-string value.</param>
    /// <exception cref="ArgumentException">The <paramref name="text"/> is not valid Unicode.</exception>
    public static string Prepare(string text)
    {
        // Follows the string preparation of RFC 4518 that every attribute type clients use shares (caseIgnoreMatch).
        var normalized = text.Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(normalized.Length);
        var pendingSpace = false;

        foreach (var c in normalized)
        {
            if (char.IsWhiteSpace(c))
            {
                if (builder.Length > 0)
                    pendingSpace = true;
            }
            else if (!char.IsControl(c) && !IsMappedToNothing(c))
            {
                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(char.ToUpperInvariant(c));
            }
        }

        return builder.ToString();
    }

    private static bool IsCharacterString(UniversalTagNumber tagNumber) =>
        tagNumber
            is UniversalTagNumber.UTF8String
                or UniversalTagNumber.PrintableString
                or UniversalTagNumber.IA5String
                or UniversalTagNumber.T61String
                or UniversalTagNumber.BMPString
                or UniversalTagNumber.UniversalString
                or UniversalTagNumber.VisibleString
                or UniversalTagNumber.NumericString;

    private static bool IsMappedToNothing(char c) =>
        c
            is '\u00AD'
                or '\u034F'
                or '\u1806'
                or (>= '\u180B' and <= '\u180D')
                or '\u200B'
                or (>= '\uFE00' and <= '\uFE0F')
                or '\uFFFC';
}
