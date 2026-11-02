// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.Secrets.KeyMaterial;

/// <summary>
/// Provides extensions methods for the <see cref="SecretKey"/> abstraction.
/// </summary>
[PublicAPI]
public static class SecretKeyExtensions
{
    extension(SecretKey secretKey)
    {
        /// <summary>
        /// Validates the <see cref="SecretKey"/> is of the expected type and size.
        /// </summary>
        /// <param name="legalKeyBitSizes">Contains a collection of valid key sizes.</param>
        /// <typeparam name="T">The expected type of the secret key.</typeparam>
        /// <returns>The secret key as the expected type.</returns>
        public T Validate<T>(IEnumerable<KeySizes> legalKeyBitSizes)
            where T : SecretKey
        {
            // CA2208 does not treat a C# 14 extension-block receiver as a parameter, but
            // `secretKey` is precisely the argument being validated, so naming it is correct.
#pragma warning disable CA2208
            if (secretKey is not T typedSecretKey)
            {
                throw new ArgumentException(
                    $"The secret key was expected to be a type of '{typeof(T).FullName}', but '{secretKey.GetType().FullName}' was given instead.",
                    nameof(secretKey)
                );
            }

            if (!KeySizesUtility.IsLegalSize(legalKeyBitSizes, secretKey.KeySizeBits))
            {
                throw new ArgumentException(
                    "The secret key does not have a valid size for this cryptographic algorithm.",
                    nameof(secretKey)
                );
            }
#pragma warning restore CA2208

            return typedSecretKey;
        }
    }
}
