// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Cryptography;
using NCode.Identity.Jose.Secp256k1.KeyTypes;
using NCode.Identity.Secrets.KeyTypes;
using Xunit;

namespace NCode.Identity.Jose.Secp256k1.Tests.KeyTypes;

public class Secp256k1CurveTests
{
    #region Specification Property Tests

    [Fact]
    public void Specification_WhenRead_NamesTheCurveAsRfc8812Registers()
    {
        var specification = Secp256k1Curve.Specification;

        Assert.Equal("secp256k1", specification.CurveName);
        Assert.Equal(256, specification.CurveSizeBits);
        Assert.Equal("1.3.132.0.10", specification.Curve.Oid.Value);
    }

    [Fact]
    public void Specification_WhenComparedWithP256_IsNotTheSameCurve()
    {
        Assert.False(
            EccCurveSpecification.IsSameNamedCurve(
                Secp256k1Curve.Specification.Curve,
                ECCurve.NamedCurves.nistP256
            )
        );
    }

    #endregion

    #region IsSupported Property Tests

    [LinuxFact]
    public void IsSupported_WhenLinux_IsTrue()
    {
        // .NET uses OpenSSL on Linux, which supports secp256k1; the gate must not hide the algorithm there.
        Assert.True(Secp256k1Curve.IsSupported);
    }

    [Fact]
    public void IsSupported_WhenRead_AnswersAsThePlatformCreatesAKey()
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
