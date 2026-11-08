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
public sealed record RemoteSigningAlgorithm : RemoteKeyAlgorithm
{
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

    /// <inheritdoc />
    public override string Use => SecretKeyUses.Signature;
}
