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
/// Represents an <c>Elliptic-Curve</c> public <c>JSON Web Key (JWK)</c> (i.e. <c>kty=EC</c>) as
/// defined by <c>RFC 7518</c>. Only the public members (<c>crv</c>, <c>x</c>, and <c>y</c>) are published.
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7518#section-6.2">RFC 7518 Section 6.2</seealso>
[PublicAPI]
public sealed class EccJsonWebKey : JsonWebKey
{
    /// <inheritdoc />
    public override string KeyType => "EC";

    /// <summary>
    /// Gets the curve name (the <c>crv</c> member), such as <c>P-256</c>, <c>P-384</c>, or <c>P-521</c>.
    /// </summary>
    /// <seealso href="https://datatracker.ietf.org/doc/html/rfc7518#section-6.2.1.1">RFC 7518 Section 6.2.1.1</seealso>
    [JsonPropertyName("crv")]
    public required string Curve { get; init; }

    /// <summary>
    /// Gets the <c>x</c> coordinate of the public point as a <c>Base64Url</c>-encoded octet string.
    /// </summary>
    /// <seealso href="https://datatracker.ietf.org/doc/html/rfc7518#section-6.2.1.2">RFC 7518 Section 6.2.1.2</seealso>
    [JsonPropertyName("x")]
    public required string X { get; init; }

    /// <summary>
    /// Gets the <c>y</c> coordinate of the public point as a <c>Base64Url</c>-encoded octet string.
    /// </summary>
    /// <seealso href="https://datatracker.ietf.org/doc/html/rfc7518#section-6.2.1.3">RFC 7518 Section 6.2.1.3</seealso>
    [JsonPropertyName("y")]
    public required string Y { get; init; }
}
