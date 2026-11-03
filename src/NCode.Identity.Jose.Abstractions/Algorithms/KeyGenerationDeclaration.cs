// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Describes a key to generate for an algorithm, or a key whose material is not at hand, by its runtime type and,
/// optionally, its size and curve, together with the use and the algorithm declared for it. <see
/// cref="AlgorithmCollectionExtensions"/> resolves it against the registered algorithms into a <see
/// cref="ResolvedKeyGeneration"/>, filling in the size and the curve the algorithm prefers when they are not given.
/// </summary>
[PublicAPI]
public readonly record struct KeyGenerationDeclaration
{
    /// <summary>
    /// Gets the runtime <see cref="Type"/> of the key (for example the <c>EccSecretKey</c> type).
    /// </summary>
    public required Type KeyClrType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key, or <c>null</c> to take the size of the named curve or, without one, the
    /// first size the algorithm accepts.
    /// </summary>
    public int? KeySizeBits { get; init; }

    /// <summary>
    /// Gets the <c>JWK</c> <c>crv</c> name of the key's curve (for example <c>P-256</c> or <c>X25519</c>), or
    /// <c>null</c> to take the first curve the algorithm accepts that has the key's size.
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
