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
/// Defines the values of the <c>kty</c> member of a <c>JSON Web Key</c> from the IANA JSON Web Key Types registry.
/// This is an extensible vocabulary of string constants, not a closed enumeration, so a contributed key type adds its
/// own name without a breaking change. <see cref="JsonWebKeyTypeDescriptors"/> holds each type's rules.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7518#section-6.1">RFC 7518 - "kty" (Key Type) Parameter Values</seealso>
[PublicAPI]
public static class JsonWebKeyTypes
{
    /// <summary>The <c>RSA</c> key type (RFC 7518 §6.3).</summary>
    public const string Rsa = "RSA";

    /// <summary>The <c>EC</c> key type, elliptic curve (RFC 7518 §6.2).</summary>
    public const string Ec = "EC";

    /// <summary>The <c>OKP</c> key type, octet key pair (RFC 8037).</summary>
    public const string Okp = "OKP";

    /// <summary>The <c>oct</c> key type, octet sequence (RFC 7518 §6.4).</summary>
    public const string Oct = "oct";

    /// <summary>The <c>AKP</c> key type, algorithm key pair (RFC 9964).</summary>
    public const string Akp = "AKP";
}
