// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.EdDsa.KeyTypes;

/// <summary>
/// Identifies what the keys of an OKP curve are for. RFC 8037 gives every OKP curve exactly one use: an Edwards curve
/// signs with <c>EdDSA</c> (§3.1), and a Montgomery curve agrees keys with <c>ECDH-ES</c> (§3.2). A key of one use is
/// never accepted for the other.
/// </summary>
// See ADR-1390.
internal enum OkpCurveUse
{
    /// <summary>
    /// The curve signs with <c>EdDSA</c>: Ed25519 or Ed448.
    /// </summary>
    Signature,

    /// <summary>
    /// The curve agrees keys with <c>ECDH-ES</c>: X25519 or X448.
    /// </summary>
    KeyAgreement,
}
