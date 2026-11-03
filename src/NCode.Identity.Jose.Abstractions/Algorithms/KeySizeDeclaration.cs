// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Describes a held key of a type without curves by its runtime type and size, together with the use and the algorithm
/// declared for it, for a key whose material is not at hand, such as a stored symmetric key.
/// <see cref="AlgorithmCollectionExtensions"/> checks it for coherence against the registered algorithms. A key of a
/// type on curves is described by a <see cref="KeyAlgorithmDeclaration"/>, which carries the key and so its curve.
/// </summary>
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
