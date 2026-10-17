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
        /// <summary>
        /// The <c>alg</c> header parameter, identifying the cryptographic algorithm used to secure the JWS or JWE.
        /// </summary>
        public const string Alg = "alg";

        /// <summary>
        /// The <c>enc</c> header parameter, identifying the content-encryption algorithm used to encrypt the JWE payload.
        /// </summary>
        public const string Enc = "enc";

        /// <summary>
        /// The <c>zip</c> header parameter, identifying the compression algorithm applied to the plaintext before encryption.
        /// </summary>
        public const string Zip = "zip";

        /// <summary>
        /// The <c>b64</c> header parameter, indicating whether the JWS payload is base64url-encoded (unencoded-payload option).
        /// </summary>
        public const string B64 = "b64";

        /// <summary>
        /// The <c>iv</c> header parameter, carrying the base64url-encoded initialization vector used for content encryption.
        /// </summary>
        public const string Iv = "iv";

        /// <summary>
        /// The <c>tag</c> header parameter, carrying the base64url-encoded authentication tag produced by content encryption.
        /// </summary>
        public const string Tag = "tag";

        /// <summary>
        /// The <c>epk</c> header parameter, carrying the ephemeral public key used by ECDH key-agreement algorithms.
        /// </summary>
        public const string Epk = "epk";

        /// <summary>
        /// The <c>kty</c> header parameter, identifying the cryptographic family of the key (for example <c>EC</c> or <c>RSA</c>).
        /// </summary>
        public const string Kty = "kty";

        /// <summary>
        /// The <c>crv</c> header parameter, identifying the elliptic curve used with an <c>EC</c> or <c>OKP</c> key.
        /// </summary>
        public const string Crv = "crv";

        /// <summary>
        /// The <c>apu</c> header parameter, carrying the base64url-encoded Agreement PartyUInfo for ECDH key agreement.
        /// </summary>
        public const string Apu = "apu";

        /// <summary>
        /// The <c>apv</c> header parameter, carrying the base64url-encoded Agreement PartyVInfo for ECDH key agreement.
        /// </summary>
        public const string Apv = "apv";

        /// <summary>
        /// The <c>x</c> header parameter, carrying the base64url-encoded x-coordinate of an elliptic-curve public key.
        /// </summary>
        public const string X = "x";

        /// <summary>
        /// The <c>y</c> header parameter, carrying the base64url-encoded y-coordinate of an elliptic-curve public key.
        /// </summary>
        public const string Y = "y";

        /// <summary>
        /// The <c>d</c> header parameter, carrying the base64url-encoded private key value of an elliptic-curve key.
        /// </summary>
        public const string D = "d";

        /// <summary>
        /// The <c>p2c</c> header parameter, carrying the PBKDF2 iteration count used by the PBES2 key-management algorithms.
        /// </summary>
        public const string P2c = "p2c";

        /// <summary>
        /// The <c>p2s</c> header parameter, carrying the base64url-encoded PBKDF2 salt input used by the PBES2 key-management algorithms.
        /// </summary>
        public const string P2s = "p2s";

        /// <summary>
        /// The <c>kid</c> header parameter, the hint identifying which key was used to secure the JWS or JWE.
        /// </summary>
        public const string Kid = "kid";

        /// <summary>
        /// The <c>jku</c> header parameter, the URL of a JWK Set containing the key used to secure the token.
        /// </summary>
        public const string Jku = "jku";

        /// <summary>
        /// The <c>jwk</c> header parameter, carrying the public key, as a JSON Web Key, used to secure the token.
        /// </summary>
        public const string Jwk = "jwk";

        /// <summary>
        /// The <c>x5u</c> header parameter, the URL of an X.509 certificate chain for the key used to secure the token.
        /// </summary>
        public const string X5u = "x5u";

        /// <summary>
        /// The <c>x5c</c> header parameter, carrying the X.509 certificate chain, as base64-encoded DER values, for the key.
        /// </summary>
        public const string X5c = "x5c";

        /// <summary>
        /// The <c>x5t</c> header parameter, carrying the base64url-encoded SHA-1 thumbprint of the X.509 certificate for the key.
        /// </summary>
        public const string X5t = "x5t";

        /// <summary>
        /// The <c>x5t#S256</c> header parameter, carrying the base64url-encoded SHA-256 thumbprint of the X.509 certificate for the key.
        /// </summary>
        public const string X5tS256 = "x5t#S256";

        /// <summary>
        /// The <c>typ</c> header parameter, declaring the media type of the complete JWS or JWE.
        /// </summary>
        public const string Typ = "typ";

        /// <summary>
        /// The <c>cty</c> header parameter, declaring the media type of the secured content (the payload).
        /// </summary>
        public const string Cty = "cty";

        /// <summary>
        /// The <c>crit</c> header parameter, listing the header parameters a recipient must understand and process.
        /// </summary>
        public const string Crit = "crit";
    }

    /// <summary>
    /// Defines the top-level member names used by the JWS and JWE JSON serializations
    /// (<c>RFC 7515</c> §7.2 and <c>RFC 7516</c> §7.2).
    /// </summary>
    [PublicAPI]
    public static class Json
    {
        /// <summary>
        /// The <c>payload</c> member, carrying the base64url-encoded JWS payload.
        /// </summary>
        public const string Payload = "payload";

        /// <summary>
        /// The <c>signatures</c> member, carrying the array of signatures in the general JWS JSON serialization.
        /// </summary>
        public const string Signatures = "signatures";

        /// <summary>
        /// The <c>signature</c> member, carrying the base64url-encoded JWS signature.
        /// </summary>
        public const string Signature = "signature";

        /// <summary>
        /// The <c>protected</c> member, carrying the base64url-encoded JOSE Protected Header.
        /// </summary>
        public const string Protected = "protected";

        /// <summary>
        /// The <c>header</c> member, carrying the per-signature or per-recipient unprotected header.
        /// </summary>
        public const string Header = "header";

        /// <summary>
        /// The <c>unprotected</c> member, carrying the JWE Shared Unprotected Header.
        /// </summary>
        public const string Unprotected = "unprotected";

        /// <summary>
        /// The <c>recipients</c> member, carrying the array of recipients in the general JWE JSON serialization.
        /// </summary>
        public const string Recipients = "recipients";

        /// <summary>
        /// The <c>encrypted_key</c> member, carrying the base64url-encoded JWE Encrypted Key for a recipient.
        /// </summary>
        public const string EncryptedKey = "encrypted_key";

        /// <summary>
        /// The <c>iv</c> member, carrying the base64url-encoded JWE Initialization Vector.
        /// </summary>
        public const string Iv = "iv";

        /// <summary>
        /// The <c>ciphertext</c> member, carrying the base64url-encoded JWE Ciphertext.
        /// </summary>
        public const string Ciphertext = "ciphertext";

        /// <summary>
        /// The <c>tag</c> member, carrying the base64url-encoded JWE Authentication Tag.
        /// </summary>
        public const string Tag = "tag";

        /// <summary>
        /// The <c>aad</c> member, carrying the base64url-encoded JWE Additional Authenticated Data.
        /// </summary>
        public const string Aad = "aad";
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
        /// Contains a constant with the value: <c>act</c>
        /// https://datatracker.ietf.org/doc/html/rfc8693#section-4.1
        /// </summary>
        public const string Act = "act";

        /// <summary>
        /// Contains a constant with the value: <c>actort</c>
        /// </summary>
        public const string Actort = "actort";

        /// <summary>
        /// Contains a constant with the value: <c>may_act</c>
        /// https://datatracker.ietf.org/doc/html/rfc8693#section-4.4
        /// </summary>
        public const string MayAct = "may_act";

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
        /// Contains a constant with the value: <c>azp</c>
        /// https://openid.net/specs/openid-connect-core-1_0.html#IDToken
        /// </summary>
        public const string Azp = "azp";

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
        /// Contains a constant with the value: <c>cnf</c>
        /// https://datatracker.ietf.org/doc/html/rfc7800#section-3.1
        /// </summary>
        public const string Cnf = "cnf";

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
        /// Contains a constant with the value: <c>nameid</c> — the legacy Microsoft JWT short name for the name
        /// identifier. The default inbound claim-type map rewrites it to
        /// <see cref="System.Security.Claims.ClaimTypes.NameIdentifier"/>, but it survives as <c>nameid</c> when a
        /// handler sets <c>MapInboundClaims</c> to <c>false</c>.
        /// </summary>
        public const string NameId = "nameid";

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
        /// Contains a constant with the value: <c>role</c>
        /// </summary>
        public const string Role = "role";

        /// <summary>
        /// Contains a constant with the value: <c>s_hash</c>
        /// https://openid.net/specs/openid-financial-api-part-2-1_0.html#id-token-as-detached-signature
        /// </summary>
        public const string SHash = "s_hash";

        /// <summary>
        /// Contains a constant with the value: <c>scope</c>
        /// https://datatracker.ietf.org/doc/html/rfc9068#section-2.2.3
        /// </summary>
        public const string Scope = "scope";

        /// <summary>
        /// Contains a constant with the value: <c>sid</c>
        /// https://openid.net/specs/openid-connect-frontchannel-1_0.html#ClaimsContents
        /// </summary>
        public const string Sid = "sid";

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
