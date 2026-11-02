// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets;

/// <summary>
/// Contains constants for the <see cref="SecretKey.KeyType"/> property.
/// </summary>
[PublicAPI]
public static class SecretKeyTypes
{
    /// <summary>
    /// Contains the constant value for a <c>symmetric</c> cryptographic key.
    /// </summary>
    public const string Symmetric = "symmetric";

    /// <summary>
    /// Contains the constant value for an <c>RSA</c> cryptographic key.
    /// </summary>
    public const string Rsa = "rsa";

    /// <summary>
    /// Contains the constant value for an <c>Elliptic-Curve</c> cryptographic key.
    /// </summary>
    public const string Ecc = "ecc";

    /// <summary>
    /// Contains the constant value for an <c>Algorithm Key Pair</c> (AKP) cryptographic key — the key type backing
    /// post-quantum <c>ML-DSA</c> (FIPS 204 / RFC 9964) signatures.
    /// </summary>
    public const string Akp = "akp";
}
