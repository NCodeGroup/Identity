// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Algorithms;
using NCode.Identity.Jose.Algorithms.Signature;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Jose.Tests.Algorithms.Signature;

/// <summary>
/// Creates an <see cref="EccSignatureAlgorithm"/> bound to a curve, sized as the curve of its hash's strength is: P-256
/// for SHA-256, P-384 for SHA-384 and P-521 for SHA-512. A curve the elliptic-curve catalog holds is named as the
/// catalog names it, which is the name a key on it carries.
/// </summary>
internal static class EccSignatureAlgorithmFactory
{
    public static EccSignatureAlgorithm Create(
        string code,
        ECCurve curve,
        HashAlgorithmName hashAlgorithmName
    )
    {
        var curveSizeBits = hashAlgorithmName.GetHashSizeBits() switch
        {
            512 => 521,
            var hashSizeBits => hashSizeBits,
        };

        var specification = new DefaultEccCurveCatalog([]).TryGetByCurve(curve, out var known)
            ? known
            : new EccCurveSpecification
            {
                CurveName = curve.Oid.FriendlyName ?? curve.Oid.Value ?? code,
                CurveSizeBits = curveSizeBits,
                Curve = curve,
            };

        return new EccSignatureAlgorithm(
            new EccSignatureAlgorithmSpecification
            {
                Code = code,
                Curve = specification,
                HashAlgorithmName = hashAlgorithmName,
            }
        );
    }
}
