// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// Immutable description of an OKP (Octet Key Pair) curve (RFC 8037): its JWK <c>crv</c> name, X.509 OID (RFC 8410),
/// what its keys are for, and the fixed byte sizes of its private key, public key and signature. The private key is
/// the seed of an Edwards curve (Ed25519, Ed448) or the scalar of a Montgomery curve (X25519, X448).
/// </summary>
internal sealed record OkpCurveInfo(
    string Name,
    string Oid,
    OkpCurveUse Use,
    int PrivateKeySize,
    int PublicKeySize,
    int SignatureSize
)
{
    public int KeySizeBits => PublicKeySize * 8;
}
