// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Asn1;

namespace NCode.Identity.OpenId.Authentication.ClientCertificates;

/// <summary>
/// Reads the RFC 5280 <c>ReasonFlags</c> as a mask: bit <c>i</c> is named bit <c>i</c>, from <c>keyCompromise</c> (1)
/// through <c>aACompromise</c> (8), and bit 0 is unused. The list of reasons is fixed by RFC 5280, so it is closed.
/// </summary>
internal static class CrlReasons
{
    /// <summary>
    /// The mask of every reason.
    /// </summary>
    public const int All = 0x1FE;

    /// <summary>
    /// Reads the reason flags that the specified tag holds and returns them as a mask.
    /// </summary>
    public static int Read(AsnReader reader, Asn1Tag tag)
    {
        var bits = reader.ReadNamedBitList(tag);
        var mask = 0;
        for (var bit = 1; bit <= 8 && bit < bits.Length; bit++)
        {
            if (bits[bit])
                mask |= 1 << bit;
        }

        return mask;
    }
}
