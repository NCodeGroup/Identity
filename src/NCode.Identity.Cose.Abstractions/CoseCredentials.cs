// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Cose.Algorithms;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Cose;

/// <summary>
/// Pairs a key with the COSE algorithm that signs, authenticates or encrypts with it.
/// </summary>
/// <param name="secretKey">The key.</param>
/// <param name="algorithm">The algorithm.</param>
[PublicAPI]
public sealed class CoseCredentials(SecretKey secretKey, CoseAlgorithm algorithm)
{
    /// <summary>
    /// Gets the key.
    /// </summary>
    public SecretKey SecretKey { get; } =
        secretKey ?? throw new ArgumentNullException(nameof(secretKey));

    /// <summary>
    /// Gets the algorithm.
    /// </summary>
    public CoseAlgorithm Algorithm { get; } =
        algorithm ?? throw new ArgumentNullException(nameof(algorithm));
}
