// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Describes a held symmetric key by its runtime type and size, together with the use and the algorithm declared for
/// it, for a key whose material is not at hand, such as a stored key. A symmetric key is a byte string, so its
/// type and size describe it whole. <see cref="AlgorithmCollectionExtensions"/> checks it for coherence against the
/// registered algorithms. An asymmetric key is described by a <see cref="KeyAlgorithmDeclaration"/>, which carries the
/// key, or its public half, and so its curve and every other parameter.
/// </summary>
// See ADR-1398.
[PublicAPI]
public readonly record struct KeySizeDeclaration
{
    /// <summary>
    /// Gets the runtime <see cref="Type"/> of the key (for example the <c>SymmetricSecretKey</c> type).
    /// </summary>
    public required Type KeyClrType { get; init; }

    /// <summary>
    /// Gets the size, in bits, of the key.
    /// </summary>
    public required int KeySizeBits { get; init; }

    /// <summary>
    /// Gets the declared use of the key (<c>sig</c> or <c>enc</c>), or <c>null</c> when none is declared.
    /// </summary>
    public required string? Use { get; init; }

    /// <summary>
    /// Gets the code of the algorithm declared for the key.
    /// </summary>
    public required string Algorithm { get; init; }
}
