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

namespace NIdentity.OpenId;

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
