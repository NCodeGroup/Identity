// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Describes an <c>ECDSA</c> <c>JWS</c> signature algorithm (RFC 7518 §3.4): its <c>alg</c> code, the one elliptic curve
/// its keys must be on, and its hash function. The <c>JOSE</c> library registers <c>ES256</c>, <c>ES384</c> and
/// <c>ES512</c> itself and adds a signature algorithm for each registered <see cref="EccSignatureAlgorithmSpecification"/>
/// service, so a package contributes an <c>ECDSA</c> algorithm, such as <c>ES256K</c> over secp256k1 (RFC 8812), as data.
/// Register the curve as an <see cref="EccCurveSpecification"/> too, so that keys on it can be generated, imported and
/// published as <c>JWK</c>s.
/// </summary>
// See ADR-1392.
[PublicAPI]
public sealed class EccSignatureAlgorithmSpecification
{
    /// <summary>
    /// Gets the algorithm's <c>alg</c> code (for example <c>ES256K</c>). No two registered algorithms share a code.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the one curve that the algorithm's keys must be on; a key on any other curve, even one of the same size, is
    /// refused.
    /// </summary>
    public required EccCurveSpecification Curve { get; init; }

    /// <summary>
    /// Gets the hash function that the algorithm signs with (for example <see cref="HashAlgorithmName.SHA256"/>).
    /// </summary>
    public required HashAlgorithmName HashAlgorithmName { get; init; }
}
