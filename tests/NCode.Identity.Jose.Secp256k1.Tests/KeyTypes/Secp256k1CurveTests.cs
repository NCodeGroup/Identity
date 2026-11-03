// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Secp256k1.KeyTypes;
using NCode.Identity.Secrets.KeyTypes;
using Xunit;

namespace NCode.Identity.Jose.Secp256k1.Tests.KeyTypes;

public class Secp256k1CurveTests
{
    #region Specification

    [Fact]
    public void Specification_NamesTheCurveAsRfc8812Registers()
    {
        var specification = Secp256k1Curve.Specification;

        Assert.Equal("secp256k1", specification.CurveName);
        Assert.Equal(256, specification.CurveSizeBits);
        Assert.Equal("1.3.132.0.10", specification.Curve.Oid.Value);
    }

    [Fact]
    public void Specification_IsNotP256_ThoughItHasTheSameSize()
    {
        Assert.False(
            EccCurveSpecification.IsSameNamedCurve(
                Secp256k1Curve.Specification.Curve,
                ECCurve.NamedCurves.nistP256
            )
        );
    }

    #endregion

    #region IsSupported

    [Fact]
    public void IsSupported_OnLinux_IsTrue()
    {
        // .NET uses OpenSSL on Linux, which supports secp256k1; the gate must not hide the algorithm there.
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        Assert.True(Secp256k1Curve.IsSupported);
    }

    [Fact]
    public void IsSupported_AnswersAsThePlatformCreatesAKey()
    {
        bool created;
        try
        {
            using var key = ECDsa.Create(ECCurve.CreateFromValue("1.3.132.0.10"));
            created = true;
        }
        catch (Exception exception)
            when (exception is CryptographicException or PlatformNotSupportedException)
        {
            created = false;
        }

        Assert.Equal(created, Secp256k1Curve.IsSupported);
    }

    #endregion
}
