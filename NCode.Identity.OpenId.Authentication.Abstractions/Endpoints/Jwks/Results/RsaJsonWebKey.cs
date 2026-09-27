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
/// Represents an <c>RSA</c> public <c>JSON Web Key (JWK)</c> (i.e. <c>kty=RSA</c>) as defined by
/// <c>RFC 7518</c>. Only the public members (<c>n</c> and <c>e</c>) are published.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7518#section-6.3">RFC 7518 Section 6.3</seealso>
[PublicAPI]
public sealed class RsaJsonWebKey : JsonWebKey
{
    /// <inheritdoc />
    public override string KeyType => "RSA";

    /// <summary>
    /// Gets the modulus (the <c>n</c> member) as a <c>Base64Url</c>-encoded unsigned big-endian integer.
    /// </summary>
    /// <seealso href="https://datatracker.ietf.org/doc/html/rfc7518#section-6.3.1.1">RFC 7518 Section 6.3.1.1</seealso>
    [JsonPropertyName("n")]
    public required string Modulus { get; init; }

    /// <summary>
    /// Gets the exponent (the <c>e</c> member) as a <c>Base64Url</c>-encoded unsigned big-endian integer.
    /// </summary>
    /// <seealso href="https://datatracker.ietf.org/doc/html/rfc7518#section-6.3.1.2">RFC 7518 Section 6.3.1.2</seealso>
    [JsonPropertyName("e")]
    public required string Exponent { get; init; }
}
