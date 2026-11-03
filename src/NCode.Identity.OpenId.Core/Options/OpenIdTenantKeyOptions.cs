// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.OpenId;

/// <summary>
/// Contains the options for one key every tenant is provisioned with. The key's intended use follows from its
/// algorithm: a signature algorithm yields a signing key, a key-management algorithm an encryption key. Its type is the
/// algorithm's first key type unless <see cref="SecretType"/> selects another the algorithm accepts.
/// </summary>
[PublicAPI]
public sealed class OpenIdTenantKeyOptions
{
    /// <summary>
    /// Gets or sets the code of the JSON Web Algorithm the key is generated for and bound to (for example
    /// <c>ES256</c>). It must name a registered signature or key-management algorithm whose key type is asymmetric.
    /// </summary>
    public string Algorithm { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the size, in bits, of the key to generate, or <c>null</c> to take the size of the key's curve,
    /// <see cref="Curve"/> or, without one, the algorithm's first curve, or, for a key type without curves, the
    /// smallest size the algorithm accepts. The size must be one the algorithm accepts, and, with a <see cref="Curve"/>,
    /// the curve's. For a key type with curves and no <see cref="Curve"/>, the key is generated on the one curve the
    /// algorithm accepts that has this size, such as X448 for a 448-bit <c>OKP</c> key for <c>ECDH-ES</c>; a size that
    /// two of the algorithm's curves have is refused, and <see cref="Curve"/> then selects one of them.
    /// </summary>
    public int? KeySizeBits { get; set; }

    /// <summary>
    /// Gets or sets the <c>JWK</c> <c>crv</c> name of the curve to generate the key on (for example <c>P-384</c> or
    /// <c>X25519</c>), or <c>null</c> to generate it on the one curve the algorithm accepts that has
    /// <see cref="KeySizeBits"/>, or on the algorithm's first curve when no size is set either. The curve must be one
    /// the algorithm accepts for the key type, and <see cref="KeySizeBits"/>, when set, must be its size. It selects
    /// among curves that share a size, such as a contributed Brainpool curve beside <c>P-256</c> for <c>ECDH-ES</c>,
    /// which a size alone does not. A stored key satisfies this key only when it is on the curve this key is generated
    /// on.
    /// </summary>
    // See ADR-1389 and ADR-1398.
    public string? Curve { get; set; }

    /// <summary>
    /// Gets or sets the secret key type to generate the key as (for example <c>ecc</c>), or <c>null</c> to generate
    /// the first key type the algorithm accepts that has a registered asymmetric secret key type and that accepts
    /// <see cref="Curve"/> and <see cref="KeySizeBits"/>; that key type then decides, so a curve or size it refuses is
    /// reported rather than generated as another key type. It selects among the key types of an algorithm that accepts
    /// several, such as <c>ECDH-ES</c>, which accepts
    /// <c>EC</c> keys and, with a package that contributes them, <c>OKP</c> keys (RFC 8037 §3.2). The type must be one
    /// the algorithm accepts.
    /// </summary>
    // See ADR-1389 and ADR-1398.
    public string? SecretType { get; set; }
}
