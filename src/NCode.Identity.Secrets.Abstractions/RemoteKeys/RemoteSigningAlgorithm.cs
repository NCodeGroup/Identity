// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes one row of the mapping from a JOSE signature algorithm to the way a key management service creates a key
/// and signs for it.
/// </summary>
// See ADR-1471.
[PublicAPI]
public sealed record RemoteSigningAlgorithm
{
    /// <summary>
    /// Gets the name of the remote key provider that the row applies to.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Gets the JOSE <c>alg</c> value of the signature algorithm.
    /// </summary>
    public required string Algorithm { get; init; }

    /// <summary>
    /// Gets the secret type of the key, such as <c>rsa</c> or <c>ecc</c>.
    /// </summary>
    public required string SecretType { get; init; }

    /// <summary>
    /// Gets the size of the key in bits.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the JWK <c>crv</c> value of the key's curve, or <see langword="null"/> when the key has no curve.
    /// </summary>
    public string? Curve { get; init; }

    /// <summary>
    /// Gets the key management service's name for the key's type and size, used when the service creates the key.
    /// </summary>
    public required string KeySpec { get; init; }

    /// <summary>
    /// Gets the key management service's name for the signing algorithm, used when the service signs.
    /// </summary>
    public required string SigningAlgorithm { get; init; }

    /// <summary>
    /// Gets the digest algorithm that the service signs, or <see langword="null"/> when the service receives the whole
    /// signing input.
    /// </summary>
    public HashAlgorithmName? DigestAlgorithm { get; init; }

    /// <summary>
    /// Gets a value indicating whether the service returns an ECDSA signature as a DER <c>SEQUENCE</c>.
    /// </summary>
    public bool IsDerEncodedSignature { get; init; }
}
