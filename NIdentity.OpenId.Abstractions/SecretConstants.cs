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
        /// Indicates that a <see cref="Secret"/> represents an <c>x509 certificate</c> secret key.
        /// The underlying key material may be <c>RSA</c> or <c>ECC</c>.
        /// </summary>
        public const string Certificate = "x509";

        /// <summary>
        /// Indicates that a <see cref="Secret"/> represents a <c>symmetric</c> secret key.
        /// </summary>
        public const string Symmetric = "symmetric";

        /// <summary>
        /// Indicates that a <see cref="Secret"/> represents an <c>RSA</c> secret key without a certificate.
        /// </summary>
        public const string Rsa = "rsa";

        /// <summary>
        /// Indicates that a <see cref="Secret"/> represents an <c>Elliptic-Curve</c> secret key without a certificate.
        /// </summary>
        public const string Ecc = "ecc";
    }

    /// <summary>
    /// Contains constants for the possible values of the <see cref="Secret.EncodingType"/> property.
    /// </summary>
    public static class EncodingTypes
    {
        /// <summary>
        /// Indicates that a <see cref="Secret"/> is not encoded and instead stored as plain-text.
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
    }
}
