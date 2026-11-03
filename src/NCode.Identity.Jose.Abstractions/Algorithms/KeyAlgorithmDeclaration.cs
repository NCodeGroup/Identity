// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

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

    /// <summary>
    /// Gets the <c>JWK</c> <c>crv</c> name of the key's curve (for example <c>P-256</c> or <c>X25519</c>), or
    /// <c>null</c> when the key has no curve or the caller does not know it, as before a key is generated without one.
    /// </summary>
    public string? Curve { get; init; }

    /// <summary>
    /// Gets the key itself, or its public half, when the caller holds it, as after a key is imported; otherwise,
    /// <c>null</c>. A key that is given is checked by the algorithm, which also refuses a key on a curve it does not
    /// accept.
    /// </summary>
    public SecretKey? Key { get; init; }
}
