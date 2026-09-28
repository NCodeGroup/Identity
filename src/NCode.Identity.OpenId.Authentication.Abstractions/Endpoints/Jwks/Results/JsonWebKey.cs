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

using System.Text.Json.Serialization;
using JetBrains.Annotations;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Results;

/// <summary>
/// Represents a single <c>JSON Web Key (JWK)</c> as defined by <c>RFC 7517</c>.
/// This is the abstract base type; the concrete key type is identified by the <see cref="KeyType"/>
/// (<c>kty</c>) member (e.g. <see cref="RsaJsonWebKey"/> for <c>RSA</c> keys and
/// <see cref="EccJsonWebKey"/> for <c>EC</c> keys).
/// </summary>
/// <remarks>
/// <para>
/// This type and its derivatives are <b>serialize-only</b> (one-way). Polymorphic serialization is
/// enabled by registering the derived types via <see cref="JsonDerivedTypeAttribute"/> <b>without</b> a
/// type discriminator, so that the emitted JSON contains only the standard JWK members (the <c>kty</c>
/// member is a normal data property, not a synthetic discriminator such as <c>$type</c>).
/// </para>
/// <para>
/// Because no type discriminator is written, these types <b>cannot be polymorphically deserialized</b>
/// through the <see cref="JsonWebKey"/> base type; attempting to do so will throw at runtime. This is
/// intentional: the types exist to produce (write) JWKS responses, not to consume (read) them. Do not
/// add a type discriminator to "enable" round-tripping without revisiting this design.
/// </para>
/// </remarks>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517#section-4">RFC 7517 Section 4</seealso>
[PublicAPI]
[JsonDerivedType(typeof(RsaJsonWebKey))]
[JsonDerivedType(typeof(EccJsonWebKey))]
public abstract class JsonWebKey
{
    /// <summary>
    /// Gets the cryptographic algorithm family (the <c>kty</c> member) that identifies the type of key,
    /// such as <c>RSA</c> or <c>EC</c>. This value also distinguishes the concrete key types within a set.
    /// </summary>
    /// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517#section-4.1">RFC 7517 Section 4.1</seealso>
    [JsonPropertyName("kty")]
    [JsonPropertyOrder(-1)]
    public abstract string KeyType { get; }

    /// <summary>
    /// Gets the intended use of the key (the <c>use</c> member), such as <c>sig</c> (signature)
    /// or <c>enc</c> (encryption). A <c>null</c> value indicates the member is omitted.
    /// </summary>
    /// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517#section-4.2">RFC 7517 Section 4.2</seealso>
    [JsonPropertyName("use")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Use { get; init; }

    /// <summary>
    /// Gets the <c>Key ID (kid)</c> member used to match a specific key.
    /// A <c>null</c> value indicates the member is omitted.
    /// </summary>
    /// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517#section-4.5">RFC 7517 Section 4.5</seealso>
    [JsonPropertyName("kid")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? KeyId { get; init; }

    /// <summary>
    /// Gets the algorithm intended for use with the key (the <c>alg</c> member), such as
    /// <c>RS256</c> or <c>ES256</c>. A <c>null</c> value indicates the member is omitted.
    /// </summary>
    /// <seealso href="https://datatracker.ietf.org/doc/html/rfc7517#section-4.4">RFC 7517 Section 4.4</seealso>
    [JsonPropertyName("alg")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Algorithm { get; init; }
}
