// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Describes a key together with the use and the algorithm declared for it, which
/// <see cref="AlgorithmCollectionExtensions"/> checks for coherence against the registered algorithms.
/// </summary>
[PublicAPI]
public readonly record struct KeyAlgorithmDeclaration
{
    /// <summary>
    /// Gets the runtime <see cref="Type"/> of the key (for example the <c>RsaSecretKey</c> type).
    /// </summary>
    public required Type KeyType { get; init; }

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
