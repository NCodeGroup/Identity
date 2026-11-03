// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Describes a key to generate for an algorithm by its runtime type and, optionally, its size and curve, together with
/// the use and the algorithm declared for it. <see cref="AlgorithmCollectionExtensions"/> resolves it against the
/// registered algorithms into a <see cref="ResolvedKeyGeneration"/>, filling in the size and the curve the algorithm
/// prefers when they are not given.
/// </summary>
[PublicAPI]
public readonly record struct KeyGenerationDeclaration
{
    /// <summary>
    /// Gets the runtime <see cref="Type"/> of the key (for example the <c>EccSecretKey</c> type).
    /// </summary>
    public required Type KeyClrType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key, or <c>null</c> to take the size of its curve: the named curve or, without
    /// one, the algorithm's first curve; for a key type without curves, <c>null</c> takes the smallest size the
    /// algorithm accepts. With a named curve, it must be the curve's size.
    /// </summary>
    public int? KeySizeBits { get; init; }

    /// <summary>
    /// Gets the <c>JWK</c> <c>crv</c> name of the key's curve (for example <c>P-256</c> or <c>X25519</c>), or
    /// <c>null</c> to take the one curve the algorithm accepts that has the key's size, which is refused when several
    /// of its curves have that size. It must be <c>null</c> for a key type the algorithm names no curve for.
    /// </summary>
    public string? Curve { get; init; }

    /// <summary>
    /// Gets the declared use of the key (<c>sig</c> or <c>enc</c>), or <c>null</c> when none is declared.
    /// </summary>
    public string? Use { get; init; }

    /// <summary>
    /// Gets the code of the algorithm declared for the key.
    /// </summary>
    public required string Algorithm { get; init; }
}
