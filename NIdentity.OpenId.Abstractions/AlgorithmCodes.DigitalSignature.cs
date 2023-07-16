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
    /// Contains constants of various supported cryptographic algorithms used for digital signatures.
    /// </summary>
    /// <remarks>
    /// References: https://datatracker.ietf.org/doc/html/rfc7518#section-3
    /// </remarks>
    public static class DigitalSignature
    {
        /// <summary>
        /// Specifies that a digital signature is not used.
        /// </summary>
        public const string None = "none";

        /// <summary>
        /// HMAC with SHA-256
        /// </summary>
        public const string HmacSha256 = "HS256";

        /// <summary>
        /// HMAC with SHA-384
        /// </summary>
        public const string HmacSha384 = "HS384";

        /// <summary>
        /// HMAC with SHA-512
        /// </summary>
        public const string HmacSha512 = "HS512";

        /// <summary>
        /// RSA Signature with SHA-256
        /// </summary>
        public const string RsaSha256 = "RS256";

        /// <summary>
        /// RSA Signature with SHA-384
        /// </summary>
        public const string RsaSha384 = "RS384";

        /// <summary>
        /// RSA Signature with SHA-512
        /// </summary>
        public const string RsaSha512 = "RS512";

        /// <summary>
        /// ECDSA using P-256 and SHA-256
        /// </summary>
        public const string EcdsaSha256 = "ES256";

        /// <summary>
        /// ECDSA using P-384 and SHA-384
        /// </summary>
        public const string EcdsaSha384 = "ES384";

        /// <summary>
        /// ECDSA using P-512 and SHA-512
        /// </summary>
        public const string EcdsaSha512 = "ES512";

        /// <summary>
        /// RSASSA-PSS using SHA-256 and MGF1 with SHA-256
        /// </summary>
        public const string RsaSsaPssSha256 = "PS256";

        /// <summary>
        /// RSASSA-PSS using SHA-384 and MGF1 with SHA-384
        /// </summary>
        public const string RsaSsaPssSha384 = "PS384";

        /// <summary>
        /// RSASSA-PSS using SHA-512 and MGF1 with SHA-512
        /// </summary>
        public const string RsaSsaPssSha512 = "PS512";
    }
}
