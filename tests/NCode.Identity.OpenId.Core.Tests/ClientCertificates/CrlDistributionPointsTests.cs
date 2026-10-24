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

using System.Security.Cryptography.X509Certificates;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class CrlDistributionPointsTests
{
    #region Read Tests

    [Fact]
    public void Read_ReturnsOnePointForEachDistributionPoint()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var certificate = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions
            {
                CrlDistributionPoints =
                [
                    "http://crl.example.test/a.crl",
                    "https://crl.example.test/b.crl",
                ],
            }
        );

        var result = CrlDistributionPoints.Read(certificate);

        Assert.Equal(2, result.Count);
        Assert.Equal(["http://crl.example.test/a.crl"], result[0].Uris);
        Assert.Equal(["https://crl.example.test/b.crl"], result[1].Uris);
        Assert.All(
            result,
            point =>
            {
                Assert.Null(point.Reasons);
                Assert.Null(point.CrlIssuer);
            }
        );
    }

    [Fact]
    public void Read_ReadsTheReasonsAndTheCrlIssuer()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var certificate = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions
            {
                ExtraExtensions =
                [
                    TestCrls.DistributionPoints(
                        TestCrls.CrlDistributionPointsOid,
                        new TestDistributionPoint(
                            "http://crl.example.test/a.crl",
                            0x006,
                            new X500DistinguishedName("CN=CRL Issuer")
                        )
                    ),
                ],
            }
        );

        var result = CrlDistributionPoints.Read(certificate);

        var point = Assert.Single(result);
        Assert.Equal(["http://crl.example.test/a.crl"], point.Uris);
        Assert.Equal(0x006, point.Reasons);
        Assert.NotNull(point.CrlIssuer);
        Assert.Equal("CN=CRL Issuer", point.CrlIssuer.Name);
    }

    [Fact]
    public void Read_SkipsAPointWithoutAnHttpLocation()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var certificate = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = ["ldap://x/y"] }
        );

        var result = CrlDistributionPoints.Read(certificate);

        Assert.Empty(result);
    }

    [Fact]
    public void Read_WithAMalformedExtension_ReturnsEmpty()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var certificate = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions
            {
                ExtraExtensions =
                [
                    new X509Extension("2.5.29.31", [0x30, 0x03, 0x02, 0x01, 0x01], false),
                ],
            }
        );

        var result = CrlDistributionPoints.Read(certificate);

        Assert.Empty(result);
    }

    #endregion

    #region ReadFreshest Tests

    [Fact]
    public void ReadFreshest_ReadsTheFreshestCrlExtension()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var certificate = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions
            {
                ExtraExtensions =
                [
                    TestCrls.DistributionPoints(
                        TestCrls.FreshestCrlOid,
                        new TestDistributionPoint("http://crl.example.test/delta.crl")
                    ),
                ],
            }
        );

        var result = CrlDistributionPoints.ReadFreshest(certificate);

        var point = Assert.Single(result);
        Assert.Equal(["http://crl.example.test/delta.crl"], point.Uris);
        Assert.Empty(CrlDistributionPoints.Read(certificate));
    }

    #endregion
}
