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

using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.ClientCertificates;

/// <summary>
/// Maps the attribute type keywords of a distinguished name string to their dotted object identifiers.
/// </summary>
internal static class DistinguishedNameAttributeTypes
{
    private const string OidPrefix = "OID.";

    // The table is closed because a dotted OID names any other attribute type (ADR-0150).
    private static readonly FrozenDictionary<string, string> Keywords = new Dictionary<
        string,
        string
    >(StringComparer.Ordinal)
    {
        ["CN"] = "2.5.4.3",
        ["SN"] = "2.5.4.4",
        ["SURNAME"] = "2.5.4.4",
        ["SERIALNUMBER"] = "2.5.4.5",
        ["C"] = "2.5.4.6",
        ["L"] = "2.5.4.7",
        ["ST"] = "2.5.4.8",
        ["S"] = "2.5.4.8",
        ["STREET"] = "2.5.4.9",
        ["O"] = "2.5.4.10",
        ["OU"] = "2.5.4.11",
        ["T"] = "2.5.4.12",
        ["TITLE"] = "2.5.4.12",
        ["DESCRIPTION"] = "2.5.4.13",
        ["BUSINESSCATEGORY"] = "2.5.4.15",
        ["POSTALCODE"] = "2.5.4.17",
        ["G"] = "2.5.4.42",
        ["GN"] = "2.5.4.42",
        ["GIVENNAME"] = "2.5.4.42",
        ["I"] = "2.5.4.43",
        ["INITIALS"] = "2.5.4.43",
        ["DNQUALIFIER"] = "2.5.4.46",
        ["PSEUDONYM"] = "2.5.4.65",
        ["ORGANIZATIONIDENTIFIER"] = "2.5.4.97",
        ["DC"] = "0.9.2342.19200300.100.1.25",
        ["UID"] = "0.9.2342.19200300.100.1.1",
        ["E"] = "1.2.840.113549.1.9.1",
        ["EMAIL"] = "1.2.840.113549.1.9.1",
        ["EMAILADDRESS"] = "1.2.840.113549.1.9.1",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>
    /// Attempts to resolve an attribute type, written either as a keyword (such as <c>CN</c>), as a dotted object
    /// identifier, or as a dotted object identifier with an <c>OID.</c> prefix, to its dotted object identifier.
    /// </summary>
    /// <param name="keyword">The attribute type as written in a distinguished name string.</param>
    /// <param name="oid">When this method returns <see langword="true"/>, the dotted object identifier.</param>
    public static bool TryGetOid(string keyword, [NotNullWhen(true)] out string? oid)
    {
        var trimmed = keyword.Trim();

        var candidate = trimmed.StartsWith(OidPrefix, StringComparison.OrdinalIgnoreCase)
            ? trimmed[OidPrefix.Length..]
            : trimmed;

        if (IsDottedOid(candidate))
        {
            oid = candidate;
            return true;
        }

        return Keywords.TryGetValue(trimmed.ToUpperInvariant(), out oid);
    }

    private static bool IsDottedOid(string value)
    {
        var arcs = value.Split('.');
        if (arcs.Length < 2)
            return false;

        foreach (var arc in arcs)
        {
            if (arc.Length == 0 || !arc.All(char.IsAsciiDigit) || (arc.Length > 1 && arc[0] == '0'))
                return false;
        }

        return arcs[0] is "0" or "1" or "2";
    }
}
