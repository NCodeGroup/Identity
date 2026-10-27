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
    public void TryGetByCurveSizeBits_ResolvesDefaultCurve()
    {
        var catalog = new DefaultEccCurveCatalog([]);

        Assert.True(catalog.TryGetByCurveSizeBits(256, out var curve));
        Assert.Equal("P-256", curve.CurveName);
        Assert.False(catalog.TryGetByCurveSizeBits(255, out _));
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

    [Fact]
    public void AdditionalCurve_IsMergedButBuiltInStaysSizeDefault()
    {
        var custom = new EccCurveSpecification
        {
            CurveName = "secp256k1",
            CurveSizeBits = 256,
            Curve = ECCurve.NamedCurves.nistP256,
        };
        var catalog = new DefaultEccCurveCatalog([custom]);

        Assert.True(catalog.TryGetByName("secp256k1", out var byName));
        Assert.Equal(256, byName.CurveSizeBits);

        // A built-in remains the size-default so the size-selected 'crv' stays stable.
        Assert.True(catalog.TryGetByCurveSizeBits(256, out var bySize));
        Assert.Equal("P-256", bySize.CurveName);
    }
}
