// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Describes a key together with the use and the algorithm declared for it, which
/// <see cref="AlgorithmCollectionExtensions"/> checks for coherence against the registered algorithms. The key's type,
/// size and curve are read from the key itself, so a declaration is checked by the same rule the cryptographic runtime
/// applies before it uses the key. A key that is still to be generated is described by a
/// <see cref="KeyGenerationDeclaration"/> instead.
/// </summary>
[PublicAPI]
public readonly record struct KeyAlgorithmDeclaration
{
    /// <summary>
    /// Gets the key, or its public half (for example a key imported from a <c>JWKS</c>, or the public half stored
    /// beside a private key).
    /// </summary>
    public required SecretKey Key { get; init; }

    /// <summary>
    /// Gets the declared use of the key (<c>sig</c> or <c>enc</c>), or <c>null</c> when none is declared.
    /// </summary>
    public required string? Use { get; init; }

    /// <summary>
    /// Gets the code of the algorithm declared for the key.
    /// </summary>
    public required string Algorithm { get; init; }
}
