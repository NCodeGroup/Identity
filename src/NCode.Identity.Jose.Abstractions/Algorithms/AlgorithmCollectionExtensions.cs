#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using JetBrains.Annotations;
using NCode.Identity.Secrets;
using NCode.Identity.Secrets.KeyMaterial;

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
        /// size. The registered algorithm metadata is the same the cryptographic runtime validates a key against, so a
        /// key that passes can be used with the algorithm it declares.
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

            if (!keyedAlgorithm.KeyType.IsAssignableFrom(declaration.KeyType))
            {
                return $"The algorithm '{algorithm}' is not compatible with the key's type.";
            }

            if (!KeySizesUtility.IsLegalSize(keyedAlgorithm.KeyBitSizes, declaration.KeySizeBits))
            {
                return $"The key size '{declaration.KeySizeBits}' bits is not valid for algorithm '{algorithm}'.";
            }

            return null;
        }
    }
}
