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

namespace NCode.Identity.OpenId;

public static partial class OpenIdProtocol
{
    /// <summary>
    /// Contains constants for various types of client authentication methods.
    /// These values are used in the <c>token_endpoint_auth_methods_supported</c> metadata.
    /// </summary>
    public static class ClientAuthenticationMethods
    {
        /// <summary>
        /// Indicates that the client does not authenticate itself, either because it uses only the Implicit Flow (and so does not use the Token Endpoint) or because it is a Public Client with no Client Secret or other authentication mechanism.
        /// </summary>
        public const string None = "none";

        /// <summary>
        /// Indicates that the authorization server uses the Client Credentials from the POST request body to authenticate the client.
        /// </summary>
        public const string ClientSecretPost = "client_secret_post";

        /// <summary>
        /// Indicates that the authorization server uses the HTTP Basic authentication scheme to authenticate the client.
        /// </summary>
        public const string ClientSecretBasic = "client_secret_basic";

        /// <summary>
        /// Indicates that the client authenticates with a JWT signed using a secret derived from the Client Secret (<c>client_secret_jwt</c>).
        /// </summary>
        public const string ClientSecretJwt = "client_secret_jwt";

        /// <summary>
        /// Indicates that the client authenticates with a JWT signed using its private key (<c>private_key_jwt</c>).
        /// </summary>
        public const string PrivateKeyJwt = "private_key_jwt";

        /// <summary>
        /// Indicates that the client authenticates with a PKI-validated mutual-TLS client certificate whose
        /// subject distinguished name or subject alternative name matches the client's registered metadata
        /// (<c>tls_client_auth</c>, RFC 8705 §2.1).
        /// </summary>
        public const string TlsClientAuth = "tls_client_auth";

        /// <summary>
        /// Indicates that the client authenticates with a mutual-TLS client certificate that matches one of the
        /// client's registered certificates by thumbprint (<c>self_signed_tls_client_auth</c>, RFC 8705 §2.2).
        /// </summary>
        public const string SelfSignedTlsClientAuth = "self_signed_tls_client_auth";
    }
}
