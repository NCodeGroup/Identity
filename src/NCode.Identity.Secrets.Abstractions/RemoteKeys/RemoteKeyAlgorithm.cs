// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Describes one row of the mapping from a JOSE algorithm to the way a key management service creates a key and
/// operates with it. Each kind of row names the operation that the service performs with the key.
/// </summary>
// See ADR-1477.
[PublicAPI]
public abstract record RemoteKeyAlgorithm
{
    /// <summary>
    /// Gets the name of the remote key provider that the row applies to.
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Gets the JOSE <c>alg</c> value of the algorithm.
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
    /// Gets the <c>use</c> of a key that the row creates, from <see cref="SecretKeyUses"/>.
    /// </summary>
    public abstract string Use { get; }
}
