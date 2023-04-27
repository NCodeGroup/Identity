#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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

namespace NCode.Jose;

public partial class AlgorithmCodes
{
    /// <summary>
    /// Contains constants of various supported cryptographic algorithms used for key management.
    /// </summary>
    /// <remarks>
    /// References: https://datatracker.ietf.org/doc/html/rfc7518#section-4
    /// </remarks>
    public static class KeyManagement
    {
        /// <summary>
        /// Direct use of a shared symmetric key as the CEK
        /// </summary>
        public const string Direct = "dir";

        /// <summary>
        /// AES Key Wrap with default initial value using 128-bit key
        /// </summary>
        public const string Aes128 = "A128KW";

        /// <summary>
        /// AES Key Wrap with default initial value using 192-bit key
        /// </summary>
        public const string Aes192 = "A192KW";

        /// <summary>
        /// AES Key Wrap with default initial value using 256-bit key
        /// </summary>
        public const string Aes256 = "A256KW";

        /// <summary>
        /// Key wrapping with AES GCM using 128-bit key
        /// </summary>
        public const string Aes128Gcm = "A128GCMKW";

        /// <summary>
        /// Key wrapping with AES GCM using 192-bit key
        /// </summary>
        public const string Aes192Gcm = "A192GCMKW";

        /// <summary>
        /// Key wrapping with AES GCM using 256-bit key
        /// </summary>
        public const string Aes256Gcm = "A256GCMKW";

        /// <summary>
        /// PBES2 with HMAC SHA-256 and A128KW wrapping
        /// </summary>
        public const string Pbes2HmacSha256Aes128 = "PBES2-HS256+A128KW";

        /// <summary>
        /// PBES2 with HMAC SHA-384 and A192KW wrapping
        /// </summary>
        public const string Pbes2HmacSha384Aes192 = "PBES2-HS384+A192KW";

        /// <summary>
        /// PBES2 with HMAC SHA-512 and A256KW wrapping
        /// </summary>
        public const string Pbes2HmacSha512Aes256 = "PBES2-HS512+A256KW";

        /// <summary>
        /// RSAES-PKCS1-v1_5
        /// </summary>
        public const string RsaPkcs1 = "RSA1_5";

        /// <summary>
        /// RSAES OAEP using default parameters
        /// </summary>
        public const string RsaOaep = "RSA-OAEP";

        /// <summary>
        /// RSAES OAEP using SHA-256 and MGF1 with SHA-256
        /// </summary>
        public const string RsaOaep256 = "RSA-OAEP-256";

        /// <summary>
        /// Elliptic Curve Diffie-Hellman Ephemeral Static key agreement using Concat KDF
        /// </summary>
        public const string EcdhEs = "ECDH-ES";

        /// <summary>
        /// ECDH-ES using Concat KDF and CEK wrapped with A128KW
        /// </summary>
        public const string EcdhEsAes128 = "ECDH-ES+A128KW";

        /// <summary>
        /// ECDH-ES using Concat KDF and CEK wrapped with A192KW
        /// </summary>
        public const string EcdhEsAes192 = "ECDH-ES+A192KW";

        /// <summary>
        /// ECDH-ES using Concat KDF and CEK wrapped with A256KW
        /// </summary>
        public const string EcdhEsAes256 = "ECDH-ES+A256KW";
    }
}
