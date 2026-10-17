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

using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// Describes a supported elliptic curve: its <c>JWK</c> <c>crv</c> name, its size in bits, and the underlying
/// <see cref="System.Security.Cryptography.ECCurve"/> used to generate and name keys. This is the single source of
/// truth for elliptic curves, shared by key generation and <c>JWK</c> publication.
/// </summary>
// See ADR-0068.
[PublicAPI]
[ExcludeFromCodeCoverage]
public sealed class EccCurveSpecification
{
    /// <summary>
    /// Gets the curve's <c>JWK</c> <c>crv</c> name (for example <c>P-256</c>, <c>P-384</c>, or <c>secp256k1</c>).
    /// </summary>
    public required string CurveName { get; init; }

    /// <summary>
    /// Gets the size of the curve, in bits (for example <c>256</c>).
    /// </summary>
    public required int CurveSizeBits { get; init; }

    /// <summary>
    /// Gets the underlying <see cref="System.Security.Cryptography.ECCurve"/>.
    /// </summary>
    public required ECCurve Curve { get; init; }
}
