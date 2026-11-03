// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Secp256k1.KeyTypes;

namespace NCode.Identity.Jose.Secp256k1.Algorithms;

/// <summary>
/// The <c>JOSE</c> algorithms over the secp256k1 curve.
/// </summary>
internal static class Secp256k1Algorithms
{
    /// <summary>
    /// Gets <c>ES256K</c>: <c>ECDSA</c> over secp256k1 with SHA-256 (RFC 8812 §3.2), bound to the curve so that a
    /// P-256 key never signs as <c>ES256K</c> and a secp256k1 key never signs as <c>ES256</c>.
    /// </summary>
    public static EccSignatureAlgorithmSpecification Es256K { get; } =
        new()
        {
            Code = AlgorithmCodes.DigitalSignature.EcdsaSecp256k1Sha256,
            Curve = Secp256k1Curve.Specification,
            HashAlgorithmName = HashAlgorithmName.SHA256,
        };
}
