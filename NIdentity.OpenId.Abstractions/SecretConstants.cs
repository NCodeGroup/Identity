#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using NIdentity.OpenId.DataContracts;

namespace NIdentity.OpenId
{
    /// <summary>
    /// Contains constants for the possible value of various <see cref="Secret"/> properties.
    /// </summary>
    public static class SecretConstants
    {
        /// <summary>
        /// Contains constants for the possible values of the <see cref="Secret.Encoding"/> property.
        /// </summary>
        public static class Encodings
        {
            /// <summary>
            /// Indicates that a <see cref="Secret"/> is encoded as a byte array using base64 encoding.
            /// </summary>
            public const string Base64 = "base64";

            /// <summary>
            /// Indicates that a <see cref="Secret"/> is encoded using the <c>PEM</c> format.
            /// </summary>
            public const string Pem = "pem";
        }

        /// <summary>
        /// Contains constants for the possible values of the <see cref="Secret.Algorithm"/> property.
        /// </summary>
        public static class Algorithms
        {
            /// <summary>
            /// Indicates that a <see cref="Secret"/> is using the <c>Advanced Encryption Standard (AES)</c> cryptographic algorithm.
            /// </summary>
            public const string Aes = "aes";

            /// <summary>
            /// Indicates that a <see cref="Secret"/> is using the <c>Rivest–Shamir–Adleman (RSA)</c> cryptographic algorithm.
            /// </summary>
            public const string Rsa = "rsa";

            /// <summary>
            /// Indicates that a <see cref="Secret"/> is using the <c>Digital Signature Algorithm (DSA)</c>.
            /// </summary>
            public const string Dsa = "dsa";

            /// <summary>
            /// Indicates that a <see cref="Secret"/> is using the <c>Elliptic Curve Digital Signature Algorithm (ECDSA)</c>.
            /// </summary>
            public const string Ecdsa = "ecdsa";

            /// <summary>
            /// Indicates that a <see cref="Secret"/> is using the <c>Elliptic-curve Diffie–Hellman (ECDH)</c> cryptographic algorithm.
            /// </summary>
            public const string Ecdh = "ecdh";
        }

        /// <summary>
        /// Contains constants for the possible values of the <see cref="Secret.Type"/> property.
        /// </summary>
        public static class Types
        {
            /// <summary>
            /// Indicates that a <see cref="Secret"/> represents a shared secret.
            /// </summary>
            public const string SharedSecret = "shared_secret";

            /// <summary>
            /// Indicates that a <see cref="Secret"/> represents a symmetric key.
            /// </summary>
            public const string SymmetricKey = "symmetric_key";

            /// <summary>
            /// Indicates that a <see cref="Secret"/> represents either a public or private key from a PKI key-pair.
            /// </summary>
            public const string AsymmetricKey = "asymmetric_key";

            /// <summary>
            /// Indicates that a <see cref="Secret"/> represents a PKI key-pair from an X509 certificate.
            /// </summary>
            public const string Certificate = "certificate";
        }
    }
}
