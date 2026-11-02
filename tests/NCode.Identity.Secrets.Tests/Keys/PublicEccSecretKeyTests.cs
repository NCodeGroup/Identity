// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Secrets.KeyTypes;

namespace NCode.Identity.Secrets.Keys;

public class PublicEccSecretKeyTests
{
    [Fact]
    public void GetECCurve_WhenSameSizeAsP256_ReturnsActualCurve()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.brainpoolP256r1);
        var key = new PublicEccSecretKey(
            new KeyMetadata { KeyId = "kid" },
            256,
            ecdsa.ExportSubjectPublicKeyInfo(),
            certificateRawData: null
        );

        var curve = key.GetECCurve();

        Assert.True(
            EccCurveSpecification.IsSameNamedCurve(curve, ECCurve.NamedCurves.brainpoolP256r1)
        );
        Assert.False(EccCurveSpecification.IsSameNamedCurve(curve, ECCurve.NamedCurves.nistP256));
    }

    [Fact]
    public void GetECCurve_WhenCalledTwice_ExportsTheKeyOnce()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var key = new CountingEccSecretKey(ecdsa);

        var first = key.GetECCurve();
        var second = key.GetECCurve();

        Assert.Equal(1, key.ExportCount);
        Assert.True(EccCurveSpecification.IsSameNamedCurve(first, ECCurve.NamedCurves.nistP256));
        Assert.True(EccCurveSpecification.IsSameNamedCurve(second, ECCurve.NamedCurves.nistP256));
    }

    private sealed class CountingEccSecretKey(ECDsa ecdsa)
        : EccSecretKey(new KeyMetadata { KeyId = "kid" }, 256, certificateRawData: null)
    {
        public int ExportCount { get; private set; }

        public override ECDsa ExportECDsa()
        {
            ExportCount++;
            return ECDsa.Create(ecdsa.ExportParameters(includePrivateParameters: true));
        }

        public override ECDiffieHellman ExportECDiffieHellman() =>
            throw new NotSupportedException();
    }
}
