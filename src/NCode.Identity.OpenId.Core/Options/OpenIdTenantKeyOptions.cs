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
    /// Gets or sets the size, in bits, of the key to generate, or <c>null</c> to generate the smallest size the
    /// algorithm accepts. The size must be one the algorithm accepts.
    /// </summary>
    public int? KeySizeBits { get; set; }

    /// <summary>
    /// Gets or sets the secret key type to generate the key as (for example <c>ecc</c>), or <c>null</c> to generate
    /// the first key type the algorithm accepts that has a registered secret key type. It selects among the key types
    /// of an algorithm that accepts several, such as <c>ECDH-ES</c>, which accepts <c>EC</c> keys and, with a package
    /// that contributes them, <c>OKP</c> keys (RFC 8037 §3.2). The type must be one the algorithm accepts.
    /// </summary>
    // See ADR-1389.
    public string? SecretType { get; set; }
}
