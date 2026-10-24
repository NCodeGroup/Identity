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
using System.Formats.Asn1;
using System.Text;

namespace NCode.Identity.OpenId.ClientCertificates;

/// <summary>
/// Parses the string representation of a distinguished name, as defined by RFC 4514, into relative distinguished
/// names. It also accepts the quoted values of RFC 2253 and ignores whitespace around separators.
/// </summary>
internal static class DistinguishedNameParser
{
    private const string EscapableLiterals = "\"+,;<>\\=# ";

    private static readonly UTF8Encoding StrictUtf8 = new(false, throwOnInvalidBytes: true);

    /// <summary>
    /// Attempts to parse the string representation of a distinguished name.
    /// </summary>
    /// <param name="value">The string representation of the distinguished name.</param>
    /// <param name="relativeNames">
    /// When this method returns <see langword="true"/>, the relative distinguished names in encoded order, which is
    /// most significant first.
    /// </param>
    public static bool TryParse(
        string value,
        [NotNullWhen(true)]
            out IReadOnlyList<IReadOnlyList<DistinguishedNameAttribute>>? relativeNames
    )
    {
        relativeNames = null;

        if (string.IsNullOrWhiteSpace(value))
            return false;

        try
        {
            var result = new List<IReadOnlyList<DistinguishedNameAttribute>>();
            var current = new List<DistinguishedNameAttribute>();
            var position = 0;

            while (true)
            {
                if (!TryReadAttribute(value, ref position, out var attribute))
                    return false;

                current.Add(attribute);

                SkipSpaces(value, ref position);
                if (position == value.Length)
                {
                    result.Add(current);
                    break;
                }

                var separator = value[position++];
                if (separator == ',')
                {
                    result.Add(current);
                    current = [];
                }
                else if (separator != '+')
                {
                    return false;
                }
            }

            // RFC 4514 writes the last relative distinguished name first
            result.Reverse();
            relativeNames = result;
            return true;
        }
        catch (Exception e)
            when (e
                    is AsnContentException
                        or FormatException
                        or DecoderFallbackException
                        or ArgumentException
            )
        {
            return false;
        }
    }

    private static bool TryReadAttribute(
        string value,
        ref int position,
        out DistinguishedNameAttribute attribute
    )
    {
        attribute = default;

        SkipSpaces(value, ref position);

        var equals = value.IndexOf('=', position);
        if (equals < 0)
            return false;

        if (!DistinguishedNameAttributeTypes.TryGetOid(value[position..equals], out var oid))
            return false;

        position = equals + 1;
        SkipSpaces(value, ref position);

        if (position < value.Length && value[position] == '#')
        {
            ++position;

            var start = position;
            while (position < value.Length && char.IsAsciiHexDigit(value[position]))
            {
                ++position;
            }

            if (position == start)
                return false;

            var encodedValue = Convert.FromHexString(value.AsSpan(start, position - start));
            attribute = DistinguishedNameAttribute.FromEncodedValue(oid, encodedValue);
            return true;
        }

        var quoted = position < value.Length && value[position] == '"';
        if (quoted)
            ++position;

        if (!TryReadText(value, ref position, quoted, out var text))
            return false;

        attribute = DistinguishedNameAttribute.FromText(oid, text);
        return true;
    }

    private static bool TryReadText(
        string value,
        ref int position,
        bool quoted,
        [NotNullWhen(true)] out string? text
    )
    {
        text = null;

        var builder = new StringBuilder();
        var pendingBytes = new List<byte>();
        var closed = false;

        while (position < value.Length)
        {
            var c = value[position];

            if (quoted)
            {
                if (c == '"')
                {
                    closed = true;
                    ++position;
                    break;
                }
            }
            else if (c is ',' or '+')
            {
                break;
            }
            else if (c is '"' or ';' or '<' or '>')
            {
                return false;
            }

            if (c == '\\')
            {
                if (!TryReadEscape(value, ref position, builder, pendingBytes))
                    return false;

                continue;
            }

            FlushPendingBytes(builder, pendingBytes);
            builder.Append(c);
            ++position;
        }

        if (quoted && !closed)
            return false;

        FlushPendingBytes(builder, pendingBytes);
        text = builder.ToString();
        return true;
    }

    private static bool TryReadEscape(
        string value,
        ref int position,
        StringBuilder builder,
        List<byte> pendingBytes
    )
    {
        if (position + 1 >= value.Length)
            return false;

        var next = value[position + 1];

        if (char.IsAsciiHexDigit(next))
        {
            if (position + 2 >= value.Length || !char.IsAsciiHexDigit(value[position + 2]))
                return false;

            pendingBytes.Add(Convert.ToByte(value.Substring(position + 1, 2), 16));
            position += 3;
            return true;
        }

        if (!EscapableLiterals.Contains(next))
            return false;

        FlushPendingBytes(builder, pendingBytes);
        builder.Append(next);
        position += 2;
        return true;
    }

    private static void FlushPendingBytes(StringBuilder builder, List<byte> pendingBytes)
    {
        if (pendingBytes.Count == 0)
            return;

        builder.Append(StrictUtf8.GetString(pendingBytes.ToArray()));
        pendingBytes.Clear();
    }

    private static void SkipSpaces(string value, ref int position)
    {
        while (position < value.Length && value[position] == ' ')
        {
            ++position;
        }
    }
}
