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

using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class CertificateRevocationListTests
{
    private const string UnknownExtensionOid = "1.2.3.4";

    private static readonly byte[] NullValue = [0x05, 0x00];

    private static byte[] BuildCrl(
        X509Certificate2 root,
        DateTimeOffset nextUpdate,
        params X509Certificate2[] revoked
    )
    {
        var builder = new CertificateRevocationListBuilder();
        foreach (var certificate in revoked)
            builder.AddEntry(certificate);

        return builder.Build(root, BigInteger.One, nextUpdate, HashAlgorithmName.SHA256);
    }

    #region TryParse Tests

    [Fact]
    public void TryParse_ABuiltCrl_ReadsItsFields()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var nextUpdate = DateTimeOffset.UtcNow.AddDays(1);
        var encoded = BuildCrl(root, nextUpdate, leaf);

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.Equal(root.SubjectName.RawData, crl.Issuer.RawData);
        Assert.InRange(
            crl.ThisUpdate,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddMinutes(5)
        );
        Assert.NotNull(crl.NextUpdate);
        Assert.Equal(nextUpdate.ToUnixTimeSeconds(), crl.NextUpdate.Value.ToUnixTimeSeconds());
        var revokedSerialNumber = Assert.Single(crl.RevokedSerialNumbers);
        Assert.Equal(
            new BigInteger(leaf.SerialNumberBytes.Span, isUnsigned: false, isBigEndian: true),
            revokedSerialNumber
        );
        Assert.False(crl.HasUnsupportedFeatures);
        Assert.Empty(crl.DistributionPointUris);
        Assert.False(crl.OnlyContainsUserCertificates);
        Assert.False(crl.OnlyContainsAuthorityCertificates);
        Assert.Equal("1.2.840.10045.4.3.2", crl.SignatureAlgorithm);
        Assert.False(crl.SignedData.IsEmpty);
        Assert.False(crl.Signature.IsEmpty);
    }

    [Fact]
    public void TryParse_Garbage_ReturnsFalse()
    {
        var result = CertificateRevocationList.TryParse(
            new byte[] { 0x01, 0x02, 0x03 },
            out var crl
        );

        Assert.False(result);
        Assert.Null(crl);
    }

    [Fact]
    public void TryParse_WithoutNextUpdate_LeavesItNull()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(root, omitNextUpdate: true);

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.Null(crl.NextUpdate);
    }

    [Fact]
    public void TryParse_WithAnUnknownCriticalExtension_IsUnsupported()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(root, extensions: [(UnknownExtensionOid, true, NullValue)]);

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.True(crl.HasUnsupportedFeatures);
    }

    [Fact]
    public void TryParse_WithAnUnknownNonCriticalExtension_IsSupported()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(root, extensions: [(UnknownExtensionOid, false, NullValue)]);

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.False(crl.HasUnsupportedFeatures);
    }

    [Fact]
    public void TryParse_WithADeltaCrlIndicator_IsUnsupported()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(
            root,
            extensions: [(TestCrls.DeltaCrlIndicatorOid, false, new byte[] { 0x02, 0x01, 0x01 })]
        );

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.True(crl.HasUnsupportedFeatures);
    }

    [Fact]
    public void TryParse_WithACriticalEntryExtension_IsUnsupported()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(
            root,
            revokedSerialNumbers: [new byte[] { 0x01, 0x02 }],
            entryExtensions: [(UnknownExtensionOid, true, NullValue)]
        );

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.True(crl.HasUnsupportedFeatures);
    }

    [Fact]
    public void TryParse_WithAnIssuingDistributionPoint_ReadsItsUri()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        const string uri = "http://crl.example.com/root.crl";
        var encoded = TestCrls.Build(
            root,
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(uri: uri, onlyContainsUserCertificates: true)
                ),
            ]
        );

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.False(crl.HasUnsupportedFeatures);
        Assert.Equal(uri, Assert.Single(crl.DistributionPointUris));
        Assert.True(crl.OnlyContainsUserCertificates);
        Assert.False(crl.OnlyContainsAuthorityCertificates);
    }

    [Fact]
    public void TryParse_WithAnIndirectIssuingDistributionPoint_IsUnsupported()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(
            root,
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(indirect: true)
                ),
            ]
        );

        var result = CertificateRevocationList.TryParse(encoded, out var crl);

        Assert.True(result);
        Assert.NotNull(crl);
        Assert.True(crl.HasUnsupportedFeatures);
    }

    #endregion

    #region IsRevoked Tests

    [Fact]
    public void IsRevoked_WhenTheSerialIsListed_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1), leaf);
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);

        var result = crl.IsRevoked(leaf);

        Assert.True(result);
    }

    [Fact]
    public void IsRevoked_WhenTheSerialIsNotListed_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        using var other = TestCertificates.CreateClientCertificate(root, "CN=d");
        var encoded = BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1), leaf);
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);

        var result = crl.IsRevoked(other);

        Assert.False(result);
    }

    #endregion

    #region Covers Tests

    private static CertificateRevocationList ParseWithIssuingDistributionPoint(
        string uri,
        bool onlyContainsUserCertificates = false
    )
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        var encoded = TestCrls.Build(
            root,
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(
                        uri: uri,
                        onlyContainsUserCertificates: onlyContainsUserCertificates
                    )
                ),
            ]
        );
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);
        return crl;
    }

    [Fact]
    public void Covers_WhenTheIssuingDistributionPointNamesTheUri_ReturnsTrue()
    {
        const string uri = "http://crl.example.com/root.crl";
        var crl = ParseWithIssuingDistributionPoint(uri);

        var result = crl.Covers(uri, isAuthority: false);

        Assert.True(result);
    }

    [Fact]
    public void Covers_WhenItNamesAnotherUri_ReturnsFalse()
    {
        var crl = ParseWithIssuingDistributionPoint("http://crl.example.com/root.crl");

        var result = crl.Covers("http://crl.example.com/other.crl", isAuthority: false);

        Assert.False(result);
    }

    [Fact]
    public void Covers_WhenOnlyUserCertificatesAndTheCertificateIsAnAuthority_ReturnsFalse()
    {
        const string uri = "http://crl.example.com/root.crl";
        var crl = ParseWithIssuingDistributionPoint(uri, onlyContainsUserCertificates: true);

        var result = crl.Covers(uri, isAuthority: true);

        Assert.False(result);
    }

    #endregion

    #region IsSignedBy Tests

    private static X509Certificate2 CreateAuthority(
        string commonName,
        X509KeyUsageFlags keyUsage,
        bool rsa = false
    )
    {
        using var rsaKey = rsa ? RSA.Create(2048) : null;
        using var ecdsaKey = rsa ? null : ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var request = rsaKey is not null
            ? new CertificateRequest(
                $"CN={commonName}",
                rsaKey,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1
            )
            : new CertificateRequest($"CN={commonName}", ecdsaKey!, HashAlgorithmName.SHA256);
        request.CertificateExtensions.Add(new X509BasicConstraintsExtension(true, false, 0, true));
        request.CertificateExtensions.Add(new X509KeyUsageExtension(keyUsage, true));
        request.CertificateExtensions.Add(
            new X509SubjectKeyIdentifierExtension(request.PublicKey, false)
        );
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddYears(1)
        );
    }

    [Fact]
    public void IsSignedBy_TheIssuingAuthority_ReturnsTrue()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1), leaf);
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);

        var result = crl.IsSignedBy(root);

        Assert.True(result);
    }

    [Fact]
    public void IsSignedBy_AnRsaAuthority_ReturnsTrue()
    {
        using var root = CreateAuthority(
            "RsaRoot",
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            rsa: true
        );
        var encoded = new CertificateRevocationListBuilder().Build(
            root,
            BigInteger.One,
            DateTimeOffset.UtcNow.AddDays(1),
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);
        Assert.Equal("1.2.840.113549.1.1.11", crl.SignatureAlgorithm);

        var result = crl.IsSignedBy(root);

        Assert.True(result);
    }

    [Theory]
    [InlineData("SHA256")]
    [InlineData("SHA384")]
    [InlineData("SHA512")]
    public void IsSignedBy_AnRsaPssAuthority_ReturnsTrue(string hashAlgorithmName)
    {
        using var root = CreateAuthority(
            "RsaPssRoot",
            X509KeyUsageFlags.KeyCertSign | X509KeyUsageFlags.CrlSign,
            rsa: true
        );
        var encoded = new CertificateRevocationListBuilder().Build(
            root,
            BigInteger.One,
            DateTimeOffset.UtcNow.AddDays(1),
            new HashAlgorithmName(hashAlgorithmName),
            RSASignaturePadding.Pss
        );
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);
        Assert.Equal("1.2.840.113549.1.1.10", crl.SignatureAlgorithm);

        var result = crl.IsSignedBy(root);

        Assert.True(result);
    }

    [Fact]
    public void IsSignedBy_AnotherAuthority_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var other = TestCertificates.CreateRootAuthority("Other");
        var encoded = BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1));
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);

        var result = crl.IsSignedBy(other);

        Assert.False(result);
    }

    [Fact]
    public void IsSignedBy_AnAuthorityWithoutCrlSign_ReturnsFalse()
    {
        using var authority = CreateAuthority("NoCrlSign", X509KeyUsageFlags.KeyCertSign);
        var encoded = TestCrls.Build(authority);
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);

        var result = crl.IsSignedBy(authority);

        Assert.False(result);
    }

    [Fact]
    public void IsSignedBy_WhenTheSignatureIsAltered_ReturnsFalse()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var encoded = BuildCrl(root, DateTimeOffset.UtcNow.AddDays(1), leaf);
        encoded[^1] ^= 0xFF;
        var parsed = CertificateRevocationList.TryParse(encoded, out var crl);
        Assert.True(parsed);
        Assert.NotNull(crl);

        var result = crl.IsSignedBy(root);

        Assert.False(result);
    }

    #endregion

    #region TryGetPssHashAlgorithm Tests

    [Fact]
    public void TryGetPssHashAlgorithm_WithSha256ParametersWithoutNull_ReturnsTheHash()
    {
        var parameters = Convert.FromHexString(
            "3030A00D300B0609608648016503040201A11A301806092A864886F70D010108300B0609608648016503040201A203020120"
        );

        var result = CertificateRevocationList.TryGetPssHashAlgorithm(
            parameters,
            out var hashAlgorithm
        );

        Assert.True(result);
        Assert.Equal(HashAlgorithmName.SHA256, hashAlgorithm);
    }

    [Fact]
    public void TryGetPssHashAlgorithm_WithNullHashParameters_ReturnsTheHash()
    {
        var parameters = Convert.FromHexString(
            "3034A00F300D06096086480165030402010500A11C301A06092A864886F70D010108300D06096086480165030402010500A203020120"
        );

        var result = CertificateRevocationList.TryGetPssHashAlgorithm(
            parameters,
            out var hashAlgorithm
        );

        Assert.True(result);
        Assert.Equal(HashAlgorithmName.SHA256, hashAlgorithm);
    }

    [Theory]
    [InlineData(
        "3030A00D300B0609608648016503040201A11A301806092A864886F70D010108300B0609608648016503040202A203020120"
    )]
    [InlineData(
        "3030A00D300B0609608648016503040201A11A301806092A864886F70D010108300B0609608648016503040201A203020121"
    )]
    [InlineData(
        "3035A00D300B0609608648016503040201A11A301806092A864886F70D010108300B0609608648016503040201A203020120A303020102"
    )]
    [InlineData(
        "3028A009300706052B0E03021AA116301406092A864886F70D010108300706052B0E03021AA203020114"
    )]
    [InlineData(
        "3030A00D300B0609608648016503040201A11A301806092A864886F70D010109300B0609608648016503040201A203020120"
    )]
    [InlineData("3000")]
    public void TryGetPssHashAlgorithm_WithParametersThatAreNotAccepted_ReturnsFalse(
        string parametersHex
    )
    {
        var parameters = Convert.FromHexString(parametersHex);

        var result = CertificateRevocationList.TryGetPssHashAlgorithm(parameters, out _);

        Assert.False(result);
    }

    #endregion
}
