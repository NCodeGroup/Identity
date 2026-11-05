// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using JetBrains.Annotations;

namespace NCode.Identity.Secrets.KeyTypes;

/// <summary>
/// Describes a supported elliptic curve: its <c>JWK</c> <c>crv</c> name, its size in bits, and the underlying
/// <see cref="System.Security.Cryptography.ECCurve"/> used to generate and name keys. This is the single source of
/// truth for elliptic curves, shared by key generation and <c>JWK</c> publication.
/// </summary>
// See ADR-1068.
[PublicAPI]
public sealed class EccCurveSpecification
{
    /// <summary>
    /// Gets the NIST <c>P-256</c> curve (<c>secp256r1</c>), of 256 bits.
    /// </summary>
    public static EccCurveSpecification P256 { get; } =
        new()
        {
            CurveName = "P-256",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.nistP256,
            Domain = EccCurveDomain.FromHex(
                "FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF",
                "FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFC",
                "5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B"
            ),
        };

    /// <summary>
    /// Gets the NIST <c>P-384</c> curve (<c>secp384r1</c>), of 384 bits.
    /// </summary>
    public static EccCurveSpecification P384 { get; } =
        new()
        {
            CurveName = "P-384",
            CurveSizeBits = 384,
            Curve = ECCurve.NamedCurves.nistP384,
            Domain = EccCurveDomain.FromHex(
                "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFFFF0000000000000000FFFFFFFF",
                "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEFFFFFFFF0000000000000000FFFFFFFC",
                "B3312FA7E23EE7E4988E056BE3F82D19181D9C6EFE8141120314088F5013875AC656398D8A2ED19D2A85C8EDD3EC2AEF"
            ),
        };

    /// <summary>
    /// Gets the NIST <c>P-521</c> curve (<c>secp521r1</c>), of 521 bits.
    /// </summary>
    public static EccCurveSpecification P521 { get; } =
        new()
        {
            CurveName = "P-521",
            CurveSizeBits = 521,
            Curve = ECCurve.NamedCurves.nistP521,
            Domain = EccCurveDomain.FromHex(
                "01FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF",
                "01FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFC",
                "0051953EB9618E1C9A1F929A21A0B68540EEA2DA725B99B315F3B8B489918EF109E156193951EC7E937B1652C0BD3BB1BF073573DF883D2C34F1EF451FD46B503F00"
            ),
        };

    /// <summary>
    /// Determines whether two named curves are the same curve, compared by OID value when both have one and otherwise
    /// by friendly name; an explicit (unnamed) curve never matches.
    /// </summary>
    /// <param name="left">The first curve to compare.</param>
    /// <param name="right">The second curve to compare.</param>
    /// <returns><see langword="true"/> when both curves are named and identify the same curve; otherwise, <see langword="false"/>.</returns>
    public static bool IsSameNamedCurve(ECCurve left, ECCurve right)
    {
        if (!left.IsNamed || !right.IsNamed)
        {
            return false;
        }

        var leftValue = left.Oid?.Value;
        var rightValue = right.Oid?.Value;
        if (!string.IsNullOrEmpty(leftValue) && !string.IsNullOrEmpty(rightValue))
        {
            return string.Equals(leftValue, rightValue, StringComparison.Ordinal);
        }

        var leftName = left.Oid?.FriendlyName;
        var rightName = right.Oid?.FriendlyName;
        return !string.IsNullOrEmpty(leftName)
            && !string.IsNullOrEmpty(rightName)
            && string.Equals(leftName, rightName, StringComparison.OrdinalIgnoreCase);
    }

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

    /// <summary>
    /// Gets the curve's domain parameters, which every imported point is checked against.
    /// </summary>
    public required EccCurveDomain Domain { get; init; }
}
