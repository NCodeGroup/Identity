// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// Immutable description of an OKP (Octet Key Pair) Edwards curve: its JWK <c>crv</c> name, X.509 OID (RFC 8410),
/// and the fixed byte sizes of its seed, public key, and signature.
/// </summary>
internal sealed record OkpCurveInfo(
    string Name,
    string Oid,
    int SeedSize,
    int PublicKeySize,
    int SignatureSize
)
{
    public int KeySizeBits => PublicKeySize * 8;
}
