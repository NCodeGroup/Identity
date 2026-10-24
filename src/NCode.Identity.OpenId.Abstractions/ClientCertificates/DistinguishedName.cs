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
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.ClientCertificates;

/// <summary>
/// Represents an X.500 distinguished name that is compared the way RFC 4517 <c>distinguishedNameMatch</c> compares
/// names: the relative distinguished names must be equal in number and order, the attributes of a multi-valued
/// relative distinguished name are a set, and string values are compared without regard to case or whitespace and
/// independently of the string encoding.
/// </summary>
[PublicAPI]
public sealed class DistinguishedName
{
    private DistinguishedName(
        IReadOnlyList<IReadOnlyList<DistinguishedNameAttribute>> relativeNames
    )
    {
        RelativeNames = relativeNames;
    }

    /// <summary>
    /// Gets the relative distinguished names in encoded order, which is most significant first.
    /// </summary>
    private IReadOnlyList<IReadOnlyList<DistinguishedNameAttribute>> RelativeNames { get; }

    /// <summary>
    /// Creates a distinguished name from an encoded X.500 name, such as the subject or the issuer of a certificate.
    /// </summary>
    /// <param name="name">The encoded X.500 name.</param>
    /// <exception cref="CryptographicException">The <paramref name="name"/> is not a valid encoded X.500 distinguished name.</exception>
    public static DistinguishedName FromX500(X500DistinguishedName name)
    {
        ArgumentNullException.ThrowIfNull(name);

        try
        {
            var reader = new AsnReader(name.RawData, AsnEncodingRules.BER);
            var sequence = reader.ReadSequence();
            reader.ThrowIfNotEmpty();

            var relativeNames = new List<IReadOnlyList<DistinguishedNameAttribute>>();
            while (sequence.HasData)
            {
                var set = sequence.ReadSetOf(skipSortOrderValidation: true);
                var attributes = new List<DistinguishedNameAttribute>();
                while (set.HasData)
                {
                    var attributeReader = set.ReadSequence();
                    var oid = attributeReader.ReadObjectIdentifier();
                    var value = attributeReader.ReadEncodedValue();
                    attributeReader.ThrowIfNotEmpty();

                    attributes.Add(DistinguishedNameAttribute.FromEncodedValue(oid, value));
                }

                if (attributes.Count == 0)
                    throw new AsnContentException("A relative distinguished name is empty.");

                relativeNames.Add(attributes);
            }

            return new DistinguishedName(relativeNames);
        }
        catch (Exception e) when (e is AsnContentException or ArgumentException)
        {
            throw new CryptographicException(
                "The name is not a valid encoded X.500 distinguished name.",
                e
            );
        }
    }

    /// <summary>
    /// Attempts to create a distinguished name from its string representation as defined by RFC 4514, such as
    /// <c>CN=client,O=Example</c>. The RFC 2253 quoted values and the <c>OID.</c> prefix of a dotted object identifier
    /// are also accepted, and whitespace around <c>=</c>, <c>,</c> and <c>+</c> is ignored.
    /// </summary>
    /// <param name="value">The string representation of the distinguished name.</param>
    /// <param name="distinguishedName">
    /// When this method returns <see langword="true"/>, the parsed distinguished name.
    /// </param>
    public static bool TryParse(
        string value,
        [NotNullWhen(true)] out DistinguishedName? distinguishedName
    )
    {
        if (DistinguishedNameParser.TryParse(value, out var relativeNames))
        {
            distinguishedName = new DistinguishedName(relativeNames);
            return true;
        }

        distinguishedName = null;
        return false;
    }

    /// <summary>
    /// Determines whether this distinguished name is equal to another one according to RFC 4517
    /// <c>distinguishedNameMatch</c>: string values are compared without regard to case or whitespace and
    /// independently of the string encoding.
    /// </summary>
    /// <param name="other">The distinguished name to compare with.</param>
    public bool Matches(DistinguishedName other)
    {
        ArgumentNullException.ThrowIfNull(other);

        if (RelativeNames.Count != other.RelativeNames.Count)
            return false;

        for (var i = 0; i < RelativeNames.Count; ++i)
        {
            if (!RelativeNameMatches(RelativeNames[i], other.RelativeNames[i]))
                return false;
        }

        return true;
    }

    private static bool RelativeNameMatches(
        IReadOnlyList<DistinguishedNameAttribute> left,
        IReadOnlyList<DistinguishedNameAttribute> right
    )
    {
        if (left.Count != right.Count)
            return false;

        var used = new bool[right.Count];
        foreach (var attribute in left)
        {
            var found = false;
            for (var i = 0; i < right.Count; ++i)
            {
                if (used[i] || !attribute.Matches(right[i]))
                    continue;

                used[i] = true;
                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }
}
