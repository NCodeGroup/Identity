// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Jose.Algorithms;

public partial class AlgorithmCodes
{
    /// <summary>
    /// Contains constants of various supported cryptographic algorithms used for authenticated encryption.
    /// </summary>
    /// <remarks>
    /// References: https://datatracker.ietf.org/doc/html/rfc7518#section-5
    /// </remarks>
    public static class AuthenticatedEncryption
    {
        /// <summary>
        /// AES_128_CBC_HMAC_SHA_256
        /// </summary>
        public const string Aes128CbcHmacSha256 = "A128CBC-HS256";

        /// <summary>
        /// AES_192_CBC_HMAC_SHA_384
        /// </summary>
        public const string Aes192CbcHmacSha384 = "A192CBC-HS384";

        /// <summary>
        /// AES_256_CBC_HMAC_SHA_512
        /// </summary>
        public const string Aes256CbcHmacSha512 = "A256CBC-HS512";

        /// <summary>
        /// AES GCM using 128-bit key
        /// </summary>
        public const string Aes128Gcm = "A128GCM";

        /// <summary>
        /// AES GCM using 192-bit key
        /// </summary>
        public const string Aes192Gcm = "A192GCM";

        /// <summary>
        /// AES GCM using 256-bit key
        /// </summary>
        public const string Aes256Gcm = "A256GCM";
    }
}
