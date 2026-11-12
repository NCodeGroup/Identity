// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Secrets.RemoteKeys;

/// <summary>
/// Identifies the <see cref="RemoteKeyAlgorithm"/> row that applies to a key.
/// </summary>
// See ADR-1471.
// See ADR-1471.
[PublicAPI]
public readonly record struct RemoteKeyAlgorithmSelector
{
    /// <summary>
    /// Gets the name of the remote key provider.
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
}
