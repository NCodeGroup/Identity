// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Asn1;
using System.Security.Cryptography;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Converts an ECDSA signature that a key management service returns as a DER <c>SEQUENCE</c> of two <c>INTEGER</c>
/// values into the IEEE P1363 <c>r‖s</c> form that JOSE and COSE carry.
/// </summary>
// See ADR-1471.
internal static class EcdsaSignatureConverter
{
    /// <summary>
    /// Converts a DER encoded ECDSA signature to the IEEE P1363 fixed-field concatenation of <c>r</c> and <c>s</c>.
    /// </summary>
    /// <param name="der">The DER encoded <c>SEQUENCE</c> of <c>r</c> and <c>s</c>.</param>
    /// <param name="fieldSizeBytes">The size in bytes of each of <c>r</c> and <c>s</c>.</param>
    /// <param name="destination">Receives <c>r</c> then <c>s</c>, each left-padded with zeros to the field size.</param>
    /// <returns>The number of bytes written, which is twice <paramref name="fieldSizeBytes"/>.</returns>
    /// <exception cref="CryptographicException">
    /// The input is not a DER <c>SEQUENCE</c> of exactly two non-negative <c>INTEGER</c> values that each fit the
    /// field, or <paramref name="destination"/> is shorter than twice <paramref name="fieldSizeBytes"/>.
    /// </exception>
    public static int ConvertDerToIeeeP1363(
        ReadOnlySpan<byte> der,
        int fieldSizeBytes,
        Span<byte> destination
    )
    {
        var signatureSizeBytes = checked(fieldSizeBytes * 2);
        if (destination.Length < signatureSizeBytes)
            throw new CryptographicException("The destination is too small for the signature.");

        ReadOnlySpan<byte> r;
        ReadOnlySpan<byte> s;
        try
        {
            AsnDecoder.ReadSequence(
                der,
                AsnEncodingRules.DER,
                out var contentOffset,
                out var contentLength,
                out var consumed
            );
            if (consumed != der.Length)
                throw new AsnContentException("The signature holds trailing data.");

            var content = der.Slice(contentOffset, contentLength);
            r = AsnDecoder.ReadIntegerBytes(content, AsnEncodingRules.DER, out var rConsumed);
            s = AsnDecoder.ReadIntegerBytes(
                content[rConsumed..],
                AsnEncodingRules.DER,
                out var sConsumed
            );
            if (rConsumed + sConsumed != contentLength)
                throw new AsnContentException("The signature holds trailing data.");
        }
        catch (AsnContentException exception)
        {
            throw new CryptographicException(
                "The signature is not a valid DER encoding.",
                exception
            );
        }

        destination = destination[..signatureSizeBytes];
        destination.Clear();
        WriteField(r, destination[..fieldSizeBytes]);
        WriteField(s, destination[fieldSizeBytes..]);
        return signatureSizeBytes;
    }

    private static void WriteField(ReadOnlySpan<byte> twosComplement, Span<byte> field)
    {
        if (twosComplement.IsEmpty || (twosComplement[0] & 0x80) != 0)
            throw new CryptographicException("The signature holds a negative integer.");

        var magnitude = twosComplement;
        while (magnitude.Length > 1 && magnitude[0] == 0)
            magnitude = magnitude[1..];

        if (magnitude.Length > field.Length)
            throw new CryptographicException(
                "The signature holds an integer that exceeds the field size."
            );

        magnitude.CopyTo(field[(field.Length - magnitude.Length)..]);
    }
}
