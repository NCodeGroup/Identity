// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// The key a <see cref="KeyGenerationDeclaration"/> resolves to: the algorithm, the use it implies, the key type of the
/// algorithm the key is of, and the size and curve the key is generated with. For a key type on curves,
/// <see cref="Curve"/> is always one of the key type's curves, so no key for a curve-bound algorithm is generated on a
/// curve the size alone selects.
/// </summary>
[PublicAPI]
public sealed class ResolvedKeyGeneration
{
    /// <summary>
    /// Gets the registered signature or key-management algorithm.
    /// </summary>
    public required KeyedAlgorithm Algorithm { get; init; }

    /// <summary>
    /// Gets the use the algorithm implies: <c>sig</c> for a signature algorithm and <c>enc</c> for a key-management
    /// algorithm.
    /// </summary>
    public required string Use { get; init; }

    /// <summary>
    /// Gets the key type of the algorithm that the key is of.
    /// </summary>
    public required AlgorithmKeyType AlgorithmKeyType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the <c>JWK</c> <c>crv</c> name of the curve the key is generated on: one of the key type's curves, or
    /// <c>null</c> for a key type without curves.
    /// </summary>
    public required string? Curve { get; init; }
}
