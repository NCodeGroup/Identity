// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Cryptography;

/// <summary>
/// Provides extension methods for the <see cref="ICryptoService"/> abstraction.
/// </summary>
[PublicAPI]
public static class CryptoServiceExtensions
{
    private const int DefaultKeyByteLength = 32;

    private const int ResourceIdByteLength = 16;

    /// <param name="cryptoService">The <see cref="ICryptoService"/> instance.</param>
    extension(ICryptoService cryptoService)
    {
        /// <summary>
        /// Generates a strong cryptographic random 256-bit value that is Base64Url encoded.
        /// </summary>
        /// <returns>The newly generated random bytes encoded as a string.</returns>
        public string GenerateUrlSafeKey() =>
            cryptoService.GenerateKey(DefaultKeyByteLength, BinaryEncodings.Base64Url);

        /// <summary>
        /// Generates a strong cryptographic random value that is Base64Url encoded.
        /// </summary>
        /// <param name="byteLength">Specifies the number of random bytes to generate. Defaults to 32 bytes.</param>
        /// <returns>The newly generated random bytes encoded as a string.</returns>
        public string GenerateUrlSafeKey(int byteLength) =>
            cryptoService.GenerateKey(byteLength, BinaryEncodings.Base64Url);

        /// <summary>
        /// Generates a new opaque canonical resource identifier (128 bits of CSPRNG entropy, Base64Url encoded).
        /// </summary>
        /// <returns>The newly generated opaque resource identifier.</returns>
        public string GenerateResourceId() =>
            cryptoService.GenerateKey(ResourceIdByteLength, BinaryEncodings.Base64Url);
    }
}
