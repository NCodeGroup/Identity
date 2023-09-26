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

using System.Diagnostics.CodeAnalysis;

namespace NCode.Jose;

/// <summary>
/// Defines some of the standard claim names used in <c>JOSE</c>.
/// </summary>
[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class JoseClaimNames
{
    /// <summary>
    /// Defines some of the standard claim names used in <c>JOSE</c> headers.
    /// </summary>
    public static class Header
    {
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

        public const string Alg = "alg";
        public const string Enc = "enc";
        public const string Zip = "zip";
        public const string B64 = "b64";

        public const string Iv = "iv";
        public const string Tag = "tag";

        public const string Epk = "epk";
        public const string Kty = "kty";
        public const string Crv = "crv";
        public const string Apu = "apu";
        public const string Apv = "apv";
        public const string X = "x";
        public const string Y = "y";
        public const string D = "d";

        public const string P2c = "p2c";
        public const string P2s = "p2s";

        public const string Kid = "kid";
        public const string Jku = "jku";
        public const string Jwk = "jwk";
        public const string X5u = "x5u";
        public const string X5c = "x5c";
        public const string X5t = "x5t";
        public const string X5tS256 = "x5t#S256";

        public const string Typ = "typ";
        public const string Cty = "cty";
        public const string Crit = "crit";

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    }

    /// <summary>
    /// Defines some of the standard claim names used in <c>JOSE</c> payloads.
    /// </summary>
    public static class Payload
    {
        /// <summary>
        /// Contains a constant with the value: <c>actort</c>
        /// </summary>
        public const string Actort = "actort";

        /// <summary>
        /// Contains a constant with the value: <c>amr</c>
        /// http://openid.net/specs/openid-connect-core-1_0.html#IDToken
        /// </summary>
        public const string Amr = "amr";

        /// <summary>
        /// Contains a constant with the value: <c>at_hash</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#HybridIDToken
        /// </summary>
        public const string AtHash = "at_hash";

        /// <summary>
        /// Contains a constant with the value: <c>aud</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        public const string Aud = "aud";

        /// <summary>
        /// Contains a constant with the value: <c>auth_time</c>
        /// http://openid.net/specs/openid-connect-core-1_0.html#IDToken
        /// </summary>
        public const string AuthTime = "auth_time";

        /// <summary>
        /// Contains a constant with the value: <c>c_hash</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#HybridIDToken
        /// </summary>
        public const string CHash = "c_hash";

        /// <summary>
        /// Contains a constant with the value: <c>exp</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        public const string Exp = "exp";

        /// <summary>
        /// Contains a constant with the value: <c>iat</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        public const string Iat = "iat";

        /// <summary>
        /// Contains a constant with the value: <c>idp</c>
        /// </summary>
        public const string Idp = "idp";

        /// <summary>
        /// Contains a constant with the value: <c>iss</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        public const string Iss = "iss";

        /// <summary>
        /// Contains a constant with the value: <c>jti</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        public const string Jti = "jti";

        /// <summary>
        /// Contains a constant with the value: <c>nbf</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        public const string Nbf = "nbf";

        /// <summary>
        /// Contains a constant with the value: <c>nonce</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#IDToken
        /// </summary>
        public const string Nonce = "nonce";

        /// <summary>
        /// Contains a constant with the value: <c>s_hash</c>
        /// https://openid.net/specs/openid-financial-api-part-2-1_0.html#id-token-as-detached-signature
        /// </summary>
        public const string SHash = "s_hash";

        /// <summary>
        /// Contains a constant with the value: <c>sub</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4
        /// </summary>
        public const string Sub = "sub";
    }
}
