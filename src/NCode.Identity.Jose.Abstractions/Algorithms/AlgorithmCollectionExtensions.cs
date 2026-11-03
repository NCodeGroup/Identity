// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using JetBrains.Annotations;
using NCode.Identity.Secrets;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Provides extension members for <see cref="IAlgorithmCollection"/>.
/// </summary>
[PublicAPI]
public static class AlgorithmCollectionExtensions
{
    /// <param name="collection">The <see cref="IAlgorithmCollection"/> of registered algorithms.</param>
    extension(IAlgorithmCollection collection)
    {
        /// <summary>
        /// Gets the registered signature or key-management algorithm with the specified code, the algorithms a key is
        /// declared for, together with the use it implies: <c>sig</c> for a signature algorithm and <c>enc</c> for a
        /// key-management algorithm.
        /// </summary>
        /// <param name="code">The algorithm's code.</param>
        /// <param name="algorithm">When this method returns <c>true</c>, the algorithm.</param>
        /// <param name="impliedUse">When this method returns <c>true</c>, the use the algorithm implies.</param>
        /// <returns><c>true</c> when a signature or key-management algorithm is registered with the code; otherwise,
        /// <c>false</c>.</returns>
        public bool TryGetKeyedAlgorithm(
            string code,
            [NotNullWhen(true)] out KeyedAlgorithm? algorithm,
            [NotNullWhen(true)] out string? impliedUse
        )
        {
            ArgumentNullException.ThrowIfNull(collection);

            if (collection.TryGetSignatureAlgorithm(code, out var signatureAlgorithm))
            {
                algorithm = signatureAlgorithm;
                impliedUse = SecretKeyUses.Signature;
                return true;
            }

            if (collection.TryGetKeyManagementAlgorithm(code, out var keyManagementAlgorithm))
            {
                algorithm = keyManagementAlgorithm;
                impliedUse = SecretKeyUses.Encryption;
                return true;
            }

            algorithm = null;
            impliedUse = null;
            return false;
        }

        /// <summary>
        /// Checks that a key may be declared for an algorithm: the algorithm is a registered signature or
        /// key-management algorithm, the use it implies (<c>sig</c> for a signature algorithm, <c>enc</c> for a
        /// key-management algorithm) agrees with the key's declared use, and the algorithm accepts the key's type, size
        /// and curve, checked against the first of its key types that the key's type is of. Two curves can share a key
        /// size, as P-256 and secp256k1 or Ed25519 and X25519 do, so a declaration that names neither the key's curve
        /// nor the key itself is checked only by type and size; one that holds the key is checked by the algorithm
        /// itself, the same check the cryptographic runtime makes before it uses the key.
        /// </summary>
        /// <param name="declaration">The key and the use and algorithm declared for it.</param>
        /// <returns><c>null</c> when the declaration is coherent; otherwise a human-readable reason.</returns>
        public string? ValidateKeyAlgorithm(KeyAlgorithmDeclaration declaration)
        {
            ArgumentNullException.ThrowIfNull(collection);

            var algorithm = declaration.Algorithm;

            if (
                !collection.TryGetKeyedAlgorithm(
                    algorithm,
                    out var keyedAlgorithm,
                    out var impliedUse
                )
            )
            {
                return $"The algorithm '{algorithm}' is not a supported signature or key-management algorithm.";
            }

            var use = declaration.Use;
            if (use is not null && !string.Equals(use, impliedUse, StringComparison.Ordinal))
            {
                return $"The algorithm '{algorithm}' is intended for '{impliedUse}' use and is incompatible with the "
                    + $"specified use '{use}'.";
            }

            if (!keyedAlgorithm.TryGetKeyType(declaration.KeyType, out var algorithmKeyType))
            {
                return $"The algorithm '{algorithm}' is not compatible with the key's type.";
            }

            if (!algorithmKeyType.IsLegalSize(declaration.KeySizeBits))
            {
                return $"The key size '{declaration.KeySizeBits}' bits is not valid for algorithm '{algorithm}'.";
            }

            // See ADR-1389: the curve separates keys that share a type and a size, such as a signing and an agreement
            // OKP key, which RFC 8037 §3.1 and §3.2 keep apart.
            if (
                declaration.Curve is { } curve
                && !algorithmKeyType.IsLegalCurve(curve, declaration.KeySizeBits)
            )
            {
                return $"The curve '{curve}' with a key size of '{declaration.KeySizeBits}' bits is not valid for "
                    + $"algorithm '{algorithm}'; expected "
                    + string.Join(
                        ", ",
                        algorithmKeyType.Curves.Select(legal =>
                            $"'{legal.Name}' ({legal.KeySizeBits} bits)"
                        )
                    )
                    + ".";
            }

            if (declaration.Key is { } key && !keyedAlgorithm.IsKeyCompatible(key))
            {
                return $"The algorithm '{algorithm}' is not compatible with the key, whose curve or parameters it does "
                    + "not accept.";
            }

            return null;
        }
    }
}
