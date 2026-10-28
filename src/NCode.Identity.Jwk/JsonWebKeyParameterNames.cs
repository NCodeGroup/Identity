#region Copyright Preamble

// Copyright @ 2026 NCode Group
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

using JetBrains.Annotations;

namespace NCode.Identity.Jwk;

/// <summary>
/// Defines the member names of a <c>JSON Web Key</c> (RFC 7517) and its key-type-specific parameters. This is an
/// extensible vocabulary of string constants, not a closed enumeration, so a custom key type can contribute members
/// without a breaking change.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517">RFC 7517 - JSON Web Key (JWK)</seealso>
[PublicAPI]
public static class JsonWebKeyParameterNames
{
    /// <summary>The <c>kty</c> member, identifying the cryptographic family of the key (RFC 7517 §4.1).</summary>
    public const string KeyType = "kty";

    /// <summary>The <c>use</c> member, identifying the intended use (<c>sig</c> / <c>enc</c>) of the key (RFC 7517 §4.2).</summary>
    public const string PublicKeyUse = "use";

    /// <summary>The <c>key_ops</c> member, identifying the operations the key is intended to be used for (RFC 7517 §4.3).</summary>
    public const string KeyOperations = "key_ops";

    /// <summary>The <c>alg</c> member, identifying the algorithm intended for use with the key (RFC 7517 §4.4).</summary>
    public const string Algorithm = "alg";

    /// <summary>The <c>kid</c> member, the identifier used to match a specific key (RFC 7517 §4.5).</summary>
    public const string KeyId = "kid";

    /// <summary>The <c>x5u</c> member, the URL of an X.509 certificate chain for the key (RFC 7517 §4.6).</summary>
    public const string X509Url = "x5u";

    /// <summary>The <c>x5c</c> member, the X.509 certificate chain as base64-encoded DER values (RFC 7517 §4.7).</summary>
    public const string X509CertificateChain = "x5c";

    /// <summary>The <c>x5t</c> member, the base64url-encoded SHA-1 thumbprint of the X.509 certificate (RFC 7517 §4.8).</summary>
    public const string X509CertificateSha1Thumbprint = "x5t";

    /// <summary>The <c>x5t#S256</c> member, the base64url-encoded SHA-256 thumbprint of the X.509 certificate (RFC 7517 §4.9).</summary>
    public const string X509CertificateSha256Thumbprint = "x5t#S256";

    /// <summary>The RSA <c>n</c> member, the base64url-encoded modulus (RFC 7518 §6.3.1.1).</summary>
    public const string Modulus = "n";

    /// <summary>The RSA <c>e</c> member, the base64url-encoded public exponent (RFC 7518 §6.3.1.2).</summary>
    public const string Exponent = "e";

    /// <summary>
    /// The RSA <c>p</c> member, the base64url-encoded first prime factor of the private key (RFC 7518 §6.3.2.2).
    /// </summary>
    public const string FirstPrimeFactor = "p";

    /// <summary>
    /// The RSA <c>q</c> member, the base64url-encoded second prime factor of the private key (RFC 7518 §6.3.2.3).
    /// </summary>
    public const string SecondPrimeFactor = "q";

    /// <summary>
    /// The RSA <c>dp</c> member, the base64url-encoded Chinese Remainder Theorem exponent of the first factor
    /// (RFC 7518 §6.3.2.4).
    /// </summary>
    public const string FirstFactorCrtExponent = "dp";

    /// <summary>
    /// The RSA <c>dq</c> member, the base64url-encoded Chinese Remainder Theorem exponent of the second factor
    /// (RFC 7518 §6.3.2.5).
    /// </summary>
    public const string SecondFactorCrtExponent = "dq";

    /// <summary>
    /// The RSA <c>qi</c> member, the base64url-encoded Chinese Remainder Theorem coefficient of the first factor
    /// (RFC 7518 §6.3.2.6).
    /// </summary>
    public const string FirstCrtCoefficient = "qi";

    /// <summary>
    /// The RSA <c>oth</c> member, an array of objects describing the other prime factors of the private key
    /// (RFC 7518 §6.3.2.7).
    /// </summary>
    public const string OtherPrimesInfo = "oth";

    /// <summary>The <c>crv</c> member, identifying the elliptic curve of an <c>EC</c> or <c>OKP</c> key (RFC 7518 / RFC 8037).</summary>
    public const string Curve = "crv";

    /// <summary>The <c>x</c> member, the base64url-encoded x-coordinate (<c>EC</c>) or public key (<c>OKP</c>).</summary>
    public const string X = "x";

    /// <summary>The <c>y</c> member, the base64url-encoded y-coordinate of an <c>EC</c> public key (RFC 7518 §6.2.1.3).</summary>
    public const string Y = "y";

    /// <summary>The <c>k</c> member, the base64url-encoded value of a symmetric (<c>oct</c>) key (RFC 7518 §6.4.1).</summary>
    public const string SymmetricKey = "k";

    /// <summary>The <c>pub</c> member, the base64url-encoded public key of an <c>AKP</c> key (RFC 9964).</summary>
    public const string PublicKey = "pub";

    /// <summary>The <c>priv</c> member, the base64url-encoded private seed of an <c>AKP</c> key (RFC 9964).</summary>
    public const string PrivateKey = "priv";

    /// <summary>
    /// The <c>d</c> member, the base64url-encoded private exponent of an <c>RSA</c> key (RFC 7518 §6.3.2.1) and the
    /// private key of an <c>EC</c> (RFC 7518 §6.2.2.1) or <c>OKP</c> (RFC 8037 §2) key.
    /// </summary>
    public const string D = "d";
}
