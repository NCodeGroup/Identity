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
    /// Contains constants for <c>DPoP</c> sender-constrained tokens
    /// (<see href="https://datatracker.ietf.org/doc/html/rfc9449">RFC 9449</see>).
    /// </summary>
    public static class DPoP
    {
        /// <summary>
        /// Contains the name of the <c>DPoP</c> HTTP request header that carries the DPoP proof JWT.
        /// </summary>
        public const string HeaderName = "DPoP";

        /// <summary>
        /// Contains the name of the <c>DPoP-Nonce</c> HTTP response header the server uses to supply a nonce the
        /// client must echo in a subsequent DPoP proof.
        /// </summary>
        public const string NonceHeaderName = "DPoP-Nonce";
    }

    /// <summary>
    /// Contains constants for the members of the <c>cnf</c> (confirmation) claim that binds a token to a
    /// proof-of-possession key (<see href="https://datatracker.ietf.org/doc/html/rfc7800">RFC 7800</see>).
    /// </summary>
    public static class ConfirmationMethods
    {
        /// <summary>
        /// Contains the name of the <c>jkt</c> confirmation member: the base64url-encoded SHA-256 JWK thumbprint
        /// (<see href="https://datatracker.ietf.org/doc/html/rfc7638">RFC 7638</see>) of the DPoP public key the
        /// access token is bound to (<see href="https://datatracker.ietf.org/doc/html/rfc9449#section-6">RFC 9449 §6</see>).
        /// </summary>
        public const string JwkThumbprint = "jkt";

        /// <summary>
        /// Contains the name of the <c>x5t#S256</c> confirmation member: the base64url-encoded SHA-256 thumbprint of
        /// the client certificate the access token is bound to
        /// (<see href="https://datatracker.ietf.org/doc/html/rfc8705#section-3.1">RFC 8705 §3.1</see>).
        /// </summary>
        public const string X509CertificateSha256Thumbprint = "x5t#S256";
    }
}
