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
using JetBrains.Annotations;

namespace NCode.Identity.Jose;

/// <summary>
/// Defines some of the standard claim names used in <c>JOSE</c>.
/// </summary>
[PublicAPI]
[SuppressMessage("ReSharper", "IdentifierTypo")]
[SuppressMessage("ReSharper", "InconsistentNaming")]
public static class JoseClaimNames
{
    /// <summary>
    /// Defines some of the standard claim names used in <c>JOSE</c> headers.
    /// </summary>
    [PublicAPI]
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
    [PublicAPI]
    public static class Payload
    {
        /// <summary>
        /// Contains a constant with the value: <c>acr</c>
        /// </summary>
        public const string Acr = "acr";

        /// <summary>
        /// Contains a constant with the value: <c>actort</c>
        /// </summary>
        public const string Actort = "actort";

        /// <summary>
        /// Contains a constant with the value: <c>address</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string Address = "address";

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
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.3
        /// </summary>
        public const string Aud = "aud";

        /// <summary>
        /// Contains a constant with the value: <c>auth_time</c>
        /// http://openid.net/specs/openid-connect-core-1_0.html#IDToken
        /// </summary>
        public const string AuthTime = "auth_time";

        /// <summary>
        /// Contains a constant with the value: <c>birthdate</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string Birthdate = "birthdate";

        /// <summary>
        /// Contains a constant with the value: <c>c_hash</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#HybridIDToken
        /// </summary>
        public const string CHash = "c_hash";

        /// <summary>
        /// Contains a constant with the value: <c>client_id</c>
        /// https://datatracker.ietf.org/doc/html/rfc9068#section-2.2
        /// </summary>
        public const string ClientId = "client_id";

        /// <summary>
        /// Contains a constant with the value: <c>email</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string Email = "email";

        /// <summary>
        /// Contains a constant with the value: <c>email_verified</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string EmailVerified = "email_verified";

        /// <summary>
        /// Contains a constant with the value: <c>exp</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.4
        /// </summary>
        public const string Exp = "exp";

        /// <summary>
        /// Contains a constant with the value: <c>family_name</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string FamilyName = "family_name";

        /// <summary>
        /// Contains a constant with the value: <c>gender</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string Gender = "gender";

        /// <summary>
        /// Contains a constant with the value: <c>given_name</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string GivenName = "given_name";

        /// <summary>
        /// Contains a constant with the value: <c>iat</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.6
        /// </summary>
        public const string Iat = "iat";

        /// <summary>
        /// Contains a constant with the value: <c>idp</c>
        /// </summary>
        public const string Idp = "idp";

        /// <summary>
        /// Contains a constant with the value: <c>iss</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.1
        /// </summary>
        public const string Iss = "iss";

        /// <summary>
        /// Contains a constant with the value: <c>jti</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.7
        /// </summary>
        public const string Jti = "jti";

        /// <summary>
        /// Contains a constant with the value: <c>locale</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string Locale = "locale";

        /// <summary>
        /// Contains a constant with the value: <c>middle_name</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string MiddleName = "middle_name";

        /// <summary>
        /// Contains a constant with the value: <c>name</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string Name = "name";

        /// <summary>
        /// Contains a constant with the value: <c>nbf</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.5
        /// </summary>
        public const string Nbf = "nbf";

        /// <summary>
        /// Contains a constant with the value: <c>nickname</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string Nickname = "nickname";

        /// <summary>
        /// Contains a constant with the value: <c>nonce</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#IDToken
        /// </summary>
        public const string Nonce = "nonce";

        /// <summary>
        /// Contains a constant with the value: <c>phone_number</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string PhoneNumber = "phone_number";

        /// <summary>
        /// Contains a constant with the value: <c>phone_number_verified</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string PhoneNumberVerified = "phone_number_verified";

        /// <summary>
        /// Contains a constant with the value: <c>picture</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string Picture = "picture";

        /// <summary>
        /// Contains a constant with the value: <c>preferred_username</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string PreferredUsername = "preferred_username";

        /// <summary>
        /// Contains a constant with the value: <c>profile</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string Profile = "profile";

        /// <summary>
        /// Contains a constant with the value: <c>s_hash</c>
        /// https://openid.net/specs/openid-financial-api-part-2-1_0.html#id-token-as-detached-signature
        /// </summary>
        public const string SHash = "s_hash";

        /// <summary>
        /// Contains a constant with the value: <c>sub</c>
        /// https://datatracker.ietf.org/doc/html/rfc7519#section-4.1.2
        /// </summary>
        public const string Sub = "sub";

        /// <summary>
        /// Contains a constant with the value: <c>tid</c> which represents the unique identifier for the tenant.
        /// </summary>
        public const string Tid = "tid";

        /// <summary>
        /// Contains a constant with the value: <c>updated_at</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string UpdatedAt = "updated_at";

        /// <summary>
        /// Contains a constant with the value: <c>website</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string Website = "website";

        /// <summary>
        /// Contains a constant with the value: <c>zoneinfo</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#StandardClaims
        /// </summary>
        public const string Zoneinfo = "zoneinfo";
    }
}
