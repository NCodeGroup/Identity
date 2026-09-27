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
using NCode.Identity.Secrets.Keys;

namespace NCode.Identity.OpenId.Authentication.Endpoints.Jwks.Converters;

/// <summary>
/// Describes a single elliptic curve that is supported for publication as a <c>JSON Web Key (JWK)</c>,
/// pairing the curve's size (used to match an <see cref="EccSecretKey"/>) with its <c>JOSE</c> curve name
/// (the JWK <c>crv</c> member value).
/// </summary>
/// <seealso href="https://datatracker.ietf.org/doc/html/rfc7518#section-6.2.1.1">RFC 7518 Section 6.2.1.1 - Curve names</seealso>
[PublicAPI]
public sealed record EccCurveSpecification
{
    /// <summary>
    /// Gets the <c>JOSE</c> curve name published as the JWK <c>crv</c> member (for example, <c>P-256</c>).
    /// </summary>
    public required string CurveName { get; init; }

    /// <summary>
    /// Gets the size of the curve, in bits (for example, <c>256</c>). This is matched against
    /// <see cref="Secrets.Keys.SecretKey.KeySizeBits"/> to select the specification for a given key.
    /// </summary>
    public required int CurveSizeBits { get; init; }
}
