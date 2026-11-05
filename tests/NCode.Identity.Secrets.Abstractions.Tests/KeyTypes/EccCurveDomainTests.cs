// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.KeyTypes;

public class EccCurveDomainTests
{
    private const string P256Prime =
        "FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFF";

    private static EccCurveDomain P256Domain { get; } =
        EccCurveDomain.FromHex(
            prime: P256Prime,
            a: "FFFFFFFF00000001000000000000000000000000FFFFFFFFFFFFFFFFFFFFFFFC",
            b: "5AC635D8AA3A93E7B3EBBD55769886BC651D06B0CC53B0F63BCE3C3E27D2604B"
        );

    private static ECPoint CreateP256Point()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return ecdsa.ExportParameters(false).Q;
    }

    #region FieldSizeBytes Property Tests

    [Fact]
    public void FieldSizeBytes_WhenP256_Returns32()
    {
        Assert.Equal(32, P256Domain.FieldSizeBytes);
    }

    #endregion

    #region ContainsPoint Tests

    [Fact]
    public void ContainsPoint_WhenPointIsOnCurve_ReturnsTrue()
    {
        var point = CreateP256Point();

        var result = P256Domain.ContainsPoint(point.X, point.Y);

        Assert.True(result);
    }

    [Fact]
    public void ContainsPoint_WhenYIsReplacedByX_ReturnsFalse()
    {
        var point = CreateP256Point();

        var result = P256Domain.ContainsPoint(point.X, point.X);

        Assert.False(result);
    }

    [Fact]
    public void ContainsPoint_WhenCoordinateIsOneByteShort_ReturnsFalse()
    {
        var point = CreateP256Point();

        var result = P256Domain.ContainsPoint(point.X.AsSpan(1), point.Y);

        Assert.False(result);
    }

    [Fact]
    public void ContainsPoint_WhenXEqualsPrime_ReturnsFalse()
    {
        var point = CreateP256Point();
        var prime = Convert.FromHexString(P256Prime);

        var result = P256Domain.ContainsPoint(prime, point.Y);

        Assert.False(result);
    }

    #endregion

    #region FromCurve Tests

    [Theory]
    [InlineData("P-256")]
    [InlineData("P-384")]
    [InlineData("P-521")]
    public void FromCurve_WhenNistCurve_EqualsTheSpecificationDomain(string curveName)
    {
        var specification = curveName switch
        {
            "P-256" => EccCurveSpecification.P256,
            "P-384" => EccCurveSpecification.P384,
            "P-521" => EccCurveSpecification.P521,
            _ => throw new ArgumentOutOfRangeException(nameof(curveName), curveName, null),
        };

        var domain = EccCurveDomain.FromCurve(specification.Curve);

        Assert.Equal(specification.Domain.Prime, domain.Prime);
        Assert.Equal(specification.Domain.A, domain.A);
        Assert.Equal(specification.Domain.B, domain.B);
    }

    [Fact]
    public void FromCurve_WhenBrainpoolP256r1_ContainsAnExportedPoint()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.brainpoolP256r1);
        var point = ecdsa.ExportParameters(false).Q;

        var domain = EccCurveDomain.FromCurve(ECCurve.NamedCurves.brainpoolP256r1);

        Assert.True(domain.ContainsPoint(point.X, point.Y));
    }

    #endregion
}
