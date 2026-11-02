// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
        /// Checks that a key may be declared for an algorithm: the algorithm is a registered signature or
        /// key-management algorithm, the use it implies (<c>sig</c> for a signature algorithm, <c>enc</c> for a
        /// key-management algorithm) agrees with the key's declared use, and the algorithm accepts the key's type and
        /// size, checked against the first of its key types that the key's type is of. The registered algorithm metadata
        /// is the same the cryptographic runtime validates a key against, so a key that passes can be used with the
        /// algorithm it declares.
        /// </summary>
        /// <param name="declaration">The key and the use and algorithm declared for it.</param>
        /// <returns><c>null</c> when the declaration is coherent; otherwise a human-readable reason.</returns>
        public string? ValidateKeyAlgorithm(KeyAlgorithmDeclaration declaration)
        {
            ArgumentNullException.ThrowIfNull(collection);

            var algorithm = declaration.Algorithm;

            KeyedAlgorithm keyedAlgorithm;
            string impliedUse;
            if (collection.TryGetSignatureAlgorithm(algorithm, out var signatureAlgorithm))
            {
                keyedAlgorithm = signatureAlgorithm;
                impliedUse = SecretKeyUses.Signature;
            }
            else if (
                collection.TryGetKeyManagementAlgorithm(algorithm, out var keyManagementAlgorithm)
            )
            {
                keyedAlgorithm = keyManagementAlgorithm;
                impliedUse = SecretKeyUses.Encryption;
            }
            else
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

            return null;
        }
    }
}
