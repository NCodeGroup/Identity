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

namespace NIdentity.OpenId;

/// <summary>
/// Contains constants for the possible value of various <see cref="Secret"/> properties.
/// </summary>
public static class SecretConstants
{
    /// <summary>
    /// Contains constants for the possible values of the <see cref="Secret.SecretType"/> property.
    /// </summary>
    public static class SecretTypes
    {
        /// <summary>
        /// Indicates that a <see cref="Secret"/> represents a shared secret.
        /// </summary>
        public const string SharedSecret = "shared_secret";

        /// <summary>
        /// Indicates that a <see cref="Secret"/> represents an <c>RSA</c> key.
        /// </summary>
        public const string Rsa = "rsa";

        /// <summary>
        /// Indicates that a <see cref="Secret"/> represents an <c>ECDSA</c> key.
        /// </summary>
        public const string Ecdsa = "ecdsa";

        /// <summary>
        /// Indicates that a <see cref="Secret"/> represents an <c>ECDH</c> key.
        /// </summary>
        public const string Ecdh = "ecdh";
    }

    /// <summary>
    /// Contains constants for the possible values of the <see cref="Secret.EncodingType"/> property.
    /// </summary>
    public static class EncodingTypes
    {
        /// <summary>
        /// Indicates that a <see cref="Secret"/> is not encoded and stored as plain-text.
        /// Primarily only used for password-based key derivation (<c>PBES2</c>).
        /// </summary>
        public const string None = "none";

        /// <summary>
        /// Indicates that a <see cref="Secret"/> is encoded as a byte array using base64 encoding.
        /// </summary>
        public const string Base64 = "base64";

        /// <summary>
        /// Indicates that a <see cref="Secret"/> is encoded using the <c>PEM</c> format.
        /// </summary>
        public const string Pem = "pem";

        /// <summary>
        /// Indicates that a <see cref="Secret"/> is encoded using the <c>JSON</c> format.
        /// </summary>
        public const string Json = "json";
    }
}
