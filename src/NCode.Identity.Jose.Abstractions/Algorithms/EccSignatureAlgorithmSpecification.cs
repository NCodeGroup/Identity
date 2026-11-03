// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Algorithms;

/// <summary>
/// Describes an <c>ECDSA</c> <c>JWS</c> signature algorithm (RFC 7518 §3.4): its <c>alg</c> code, the one elliptic
/// curve its keys must be on, and its hash function. The <c>JOSE</c> library adds a signature algorithm for each
/// registered <see cref="EccSignatureAlgorithmSpecification"/> service and registers <see cref="Es256"/>,
/// <see cref="Es384"/> and <see cref="Es512"/> itself, so a package contributes an <c>ECDSA</c> algorithm, such as
/// <c>ES256K</c> over secp256k1 (RFC 8812), as data. The algorithm's curve must be registered as an
/// <see cref="EccCurveSpecification"/> too, so that keys on it can be generated, imported and published as
/// <c>JWK</c>s; the library refuses to compose an algorithm whose curve is not.
/// </summary>
// See ADR-1392.
[PublicAPI]
public sealed class EccSignatureAlgorithmSpecification
{
    /// <summary>
    /// Gets <c>ES256</c>: <c>ECDSA</c> over <c>P-256</c> with SHA-256 (RFC 7518 §3.4).
    /// </summary>
    public static EccSignatureAlgorithmSpecification Es256 { get; } =
        new()
        {
            Code = AlgorithmCodes.DigitalSignature.EcdsaSha256,
            Curve = EccCurveSpecification.P256,
            HashAlgorithmName = HashAlgorithmName.SHA256,
        };

    /// <summary>
    /// Gets <c>ES384</c>: <c>ECDSA</c> over <c>P-384</c> with SHA-384 (RFC 7518 §3.4).
    /// </summary>
    public static EccSignatureAlgorithmSpecification Es384 { get; } =
        new()
        {
            Code = AlgorithmCodes.DigitalSignature.EcdsaSha384,
            Curve = EccCurveSpecification.P384,
            HashAlgorithmName = HashAlgorithmName.SHA384,
        };

    /// <summary>
    /// Gets <c>ES512</c>: <c>ECDSA</c> over <c>P-521</c> with SHA-512 (RFC 7518 §3.4).
    /// </summary>
    public static EccSignatureAlgorithmSpecification Es512 { get; } =
        new()
        {
            Code = AlgorithmCodes.DigitalSignature.EcdsaSha512,
            Curve = EccCurveSpecification.P521,
            HashAlgorithmName = HashAlgorithmName.SHA512,
        };

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
