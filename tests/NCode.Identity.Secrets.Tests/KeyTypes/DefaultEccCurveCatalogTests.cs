// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;

namespace NCode.Identity.Secrets.KeyTypes;

public class DefaultEccCurveCatalogTests
{
    [Fact]
    public void BuiltInCurves_AreAvailableByName()
    {
        var catalog = new DefaultEccCurveCatalog([]);

        Assert.True(catalog.TryGetByName("P-256", out var p256));
        Assert.Equal(256, p256.CurveSizeBits);
        Assert.True(catalog.TryGetByName("P-384", out _));
        Assert.True(catalog.TryGetByName("P-521", out _));
        Assert.False(catalog.TryGetByName("not-a-curve", out _));
    }

    [Fact]
    public void GetByCurveSizeBits_ListsTheCurvesOfTheSize()
    {
        var catalog = new DefaultEccCurveCatalog([]);

        var curve = Assert.Single(catalog.GetByCurveSizeBits(256));
        Assert.Equal("P-256", curve.CurveName);
        Assert.Empty(catalog.GetByCurveSizeBits(255));
    }

    [Fact]
    public void TryGetByCurve_MatchesByNamedCurveOid()
    {
        var catalog = new DefaultEccCurveCatalog([]);

        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);

        Assert.True(catalog.TryGetByCurve(parameters.Curve, out var curve));
        Assert.Equal("P-256", curve.CurveName);
    }

    [Fact]
    public void IsSameNamedCurve_WhenExportedFromSameCurve_ReturnsTrue()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var parameters = ecdsa.ExportParameters(includePrivateParameters: false);

        Assert.True(
            EccCurveSpecification.IsSameNamedCurve(parameters.Curve, ECCurve.NamedCurves.nistP256)
        );
    }

    [Fact]
    public void IsSameNamedCurve_WhenDifferentCurveOfSameSize_ReturnsFalse()
    {
        Assert.False(
            EccCurveSpecification.IsSameNamedCurve(
                ECCurve.NamedCurves.nistP256,
                ECCurve.NamedCurves.brainpoolP256r1
            )
        );
    }

    // See ADR-1398: two curves can share a size, and the catalog lists both, the built-in first, so the key type can
    // refuse the size until the curve is named.
    [Fact]
    public void AdditionalCurve_IsMergedAfterTheBuiltInOfItsSize()
    {
        var custom = new EccCurveSpecification
        {
            CurveName = "secp256k1",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.nistP256,
            Domain = EccCurveDomain.FromCurve(ECCurve.NamedCurves.nistP256),
        };
        var catalog = new DefaultEccCurveCatalog([custom]);

        Assert.True(catalog.TryGetByName("secp256k1", out var byName));
        Assert.Equal(256, byName.CurveSizeBits);
        Assert.Equal(
            ["P-256", "secp256k1"],
            catalog.GetByCurveSizeBits(256).Select(curve => curve.CurveName)
        );
    }
}
