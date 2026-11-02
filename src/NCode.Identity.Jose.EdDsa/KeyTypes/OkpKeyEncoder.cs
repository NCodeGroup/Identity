// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Formats.Asn1;

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// Encodes and decodes OKP keys in the standard <c>SubjectPublicKeyInfo</c> (SPKI) and <c>PKCS#8</c> /
/// <c>OneAsymmetricKey</c> DER structures defined by RFC 8410 (X25519 OID <c>1.3.101.110</c>, X448
/// <c>1.3.101.111</c>, Ed25519 <c>1.3.101.112</c>, Ed448 <c>1.3.101.113</c>). The PKCS#8 private key wraps the raw
/// private key (the seed of an Edwards curve, the scalar of a Montgomery curve) in a nested <c>OCTET STRING</c>.
/// </summary>
internal static class OkpKeyEncoder
{
    /// <summary>Writes the SPKI (<c>SubjectPublicKeyInfo</c>) DER for the raw public key to the buffer writer.</summary>
    public static void WriteSubjectPublicKeyInfo<TWriter>(
        OkpCurveInfo curve,
        ReadOnlySpan<byte> publicKey,
        ref TWriter destination
    )
        where TWriter : IBufferWriter<byte>, allows ref struct
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (writer.PushSequence())
            {
                writer.WriteObjectIdentifier(curve.Oid);
            }

            writer.WriteBitString(publicKey);
        }

        var length = writer.GetEncodedLength();
        var span = destination.GetSpan(length);
        var written = writer.Encode(span);
        destination.Advance(written);
    }

    /// <summary>Builds the PKCS#8 (<c>OneAsymmetricKey</c>) DER for the raw private seed.</summary>
    public static byte[] BuildPkcs8(OkpCurveInfo curve, ReadOnlySpan<byte> seed)
    {
        // The privateKey OCTET STRING contains an inner CurvePrivateKey ::= OCTET STRING of the seed (RFC 8410 §7).
        var inner = new AsnWriter(AsnEncodingRules.DER);
        inner.WriteOctetString(seed);
        var innerEncoded = inner.Encode();
        try
        {
            var writer = new AsnWriter(AsnEncodingRules.DER);
            using (writer.PushSequence())
            {
                writer.WriteInteger(0);
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(curve.Oid);
                }

                writer.WriteOctetString(innerEncoded);
            }

            return writer.Encode();
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(innerEncoded);
        }
    }

    /// <summary>Parses an SPKI DER into the curve and raw public key, or returns <c>false</c> when it is not a
    /// supported OKP key or is malformed.</summary>
    public static bool TryParseSubjectPublicKeyInfo(
        ReadOnlySpan<byte> spki,
        out OkpCurveInfo? curve,
        out byte[]? publicKey
    )
    {
        curve = null;
        publicKey = null;
        try
        {
            var reader = new AsnReader(spki.ToArray(), AsnEncodingRules.DER);
            var sequence = reader.ReadSequence();
            var algorithm = sequence.ReadSequence();
            var oid = algorithm.ReadObjectIdentifier();
            if (algorithm.HasData || !OkpCurves.TryGetByOid(oid, out var info))
            {
                return false;
            }

            var key = sequence.ReadBitString(out var unusedBitCount);
            if (unusedBitCount != 0 || sequence.HasData || key.Length != info.PublicKeySize)
            {
                return false;
            }

            reader.ThrowIfNotEmpty();
            curve = info;
            publicKey = key;
            return true;
        }
        catch (AsnContentException)
        {
            return false;
        }
    }

    /// <summary>Parses a PKCS#8 DER into the curve and raw seed, or returns <c>false</c> when malformed or
    /// unsupported.</summary>
    public static bool TryParsePkcs8(
        ReadOnlySpan<byte> pkcs8,
        out OkpCurveInfo? curve,
        out byte[]? seed
    )
    {
        curve = null;
        seed = null;
        try
        {
            var reader = new AsnReader(pkcs8.ToArray(), AsnEncodingRules.DER);
            var sequence = reader.ReadSequence();
            _ = sequence.ReadInteger(); // version
            var algorithm = sequence.ReadSequence();
            var oid = algorithm.ReadObjectIdentifier();
            if (algorithm.HasData || !OkpCurves.TryGetByOid(oid, out var info))
            {
                return false;
            }

            var privateKeyOctets = sequence.ReadOctetString();
            var inner = new AsnReader(privateKeyOctets, AsnEncodingRules.DER);
            var rawSeed = inner.ReadOctetString();
            inner.ThrowIfNotEmpty();
            if (rawSeed.Length != info.PrivateKeySize)
            {
                return false;
            }

            curve = info;
            seed = rawSeed;
            return true;
        }
        catch (AsnContentException)
        {
            return false;
        }
    }
}
