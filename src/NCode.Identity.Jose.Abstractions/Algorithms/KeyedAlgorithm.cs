// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Common abstractions for all <c>JOSE</c> algorithms that use a <see cref="SecretKey"/>.
/// </summary>
[PublicAPI]
public abstract class KeyedAlgorithm : Algorithm
{
    /// <summary>
    /// Gets the types of key that the current cryptographic algorithm accepts, each with the sizes it accepts keys of
    /// that type in, in the order of preference. Most algorithms accept one key type; one whose name serves several,
    /// such as <c>ECDH-ES</c> over <c>EC</c> and <c>OKP</c> keys (RFC 8037 §3.2), lists each of them. The first is the
    /// one a key is provisioned as when nothing else selects one.
    /// </summary>
    // See ADR-1389.
    public abstract IReadOnlyList<AlgorithmKeyType> KeyTypes { get; }

    /// <summary>
    /// Gets the first of <see cref="KeyTypes"/> that keys of the specified runtime type are of.
    /// </summary>
    /// <param name="keyClrType">The runtime type of a key.</param>
    /// <param name="algorithmKeyType">When this method returns <c>true</c>, the matching key type.</param>
    /// <returns><c>true</c> when the algorithm accepts keys of the type; otherwise, <c>false</c>.</returns>
    public bool TryGetKeyType(
        Type keyClrType,
        [NotNullWhen(true)] out AlgorithmKeyType? algorithmKeyType
    )
    {
        ArgumentNullException.ThrowIfNull(keyClrType);

        foreach (var candidate in KeyTypes)
        {
            if (candidate.Accepts(keyClrType))
            {
                algorithmKeyType = candidate;
                return true;
            }
        }

        algorithmKeyType = null;
        return false;
    }

    /// <summary>
    /// Determines whether every key type the algorithm accepts derives from <typeparamref name="TKey"/>, for example
    /// whether the algorithm is keyed only by asymmetric keys.
    /// </summary>
    /// <typeparam name="TKey">The base <see cref="SecretKey"/> type.</typeparam>
    /// <returns><c>true</c> when the algorithm accepts at least one key type and each derives from
    /// <typeparamref name="TKey"/>; otherwise, <c>false</c>.</returns>
    public bool AcceptsOnly<TKey>()
        where TKey : SecretKey =>
        KeyTypes.Count > 0
        && KeyTypes.All(algorithmKeyType =>
            typeof(TKey).IsAssignableFrom(algorithmKeyType.KeyClrType)
        );

    /// <summary>
    /// Determines whether a secret key can be used with this algorithm. The default checks the key against the first of
    /// <see cref="KeyTypes"/> that its runtime type is of, with <see cref="AlgorithmKeyType.IsKeyCompatible"/>: its
    /// size, and for a key type on curves its curve, must be one that key type accepts. An algorithm overrides it only
    /// to add a check its key types cannot express, such as a key agreement that must serve the key.
    /// </summary>
    /// <param name="secretKey">The <see cref="SecretKey"/> to check.</param>
    /// <returns><c>true</c> if the key can be used with this algorithm; otherwise, <c>false</c>.</returns>
    public virtual bool IsKeyCompatible(SecretKey secretKey)
    {
        ArgumentNullException.ThrowIfNull(secretKey);

        return TryGetKeyType(secretKey.GetType(), out var algorithmKeyType)
            && algorithmKeyType.IsKeyCompatible(secretKey);
    }
}
