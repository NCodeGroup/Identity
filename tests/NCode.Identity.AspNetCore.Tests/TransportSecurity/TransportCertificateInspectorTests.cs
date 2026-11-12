// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Formats.Asn1;
using System.Security.Cryptography;
using NCode.Identity.AspNetCore.TransportSecurity;
using Xunit;

namespace NCode.Identity.AspNetCore.Tests.TransportSecurity;

public sealed class TransportCertificateInspectorTests
{
    private static DateTimeOffset Now { get; } = new(2030, 6, 15, 12, 0, 0, TimeSpan.Zero);

    #region FindFailures Tests

    [Fact]
    public void FindFailures_WhenTheCertificateIsValid_ReturnsNone()
    {
        using var ecdsa = TransportSecurityTestCertificates.CreateValid(Now);
        using var rsa = TransportSecurityTestCertificates.CreateRsa(
            2048,
            HashAlgorithmName.SHA256,
            Now
        );

        Assert.Empty(TransportCertificateInspector.FindFailures(ecdsa, Now));
        Assert.Empty(TransportCertificateInspector.FindFailures(rsa, Now));
    }

    [Fact]
    public void FindFailures_WhenTheCertificateHasExpired_ReportsExpiry()
    {
        using var certificate = TransportSecurityTestCertificates.CreateWithValidity(
            Now.AddDays(-30),
            Now.AddDays(-1)
        );

        var failures = TransportCertificateInspector.FindFailures(certificate, Now);

        var failure = Assert.Single(failures);
        Assert.StartsWith("expired at ", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void FindFailures_WhenTheCertificateIsNotYetValid_ReportsNotValidBefore()
    {
        using var certificate = TransportSecurityTestCertificates.CreateWithValidity(
            Now.AddDays(1),
            Now.AddDays(30)
        );

        var failures = TransportCertificateInspector.FindFailures(certificate, Now);

        var failure = Assert.Single(failures);
        Assert.StartsWith("is not valid before ", failure, StringComparison.Ordinal);
    }

    [Fact]
    public void FindFailures_WhenTheRsaKeyIsBelow2048Bits_ReportsTheKeySize()
    {
        using var certificate = TransportSecurityTestCertificates.CreateRsa(
            1024,
            HashAlgorithmName.SHA256,
            Now
        );

        var failures = TransportCertificateInspector.FindFailures(certificate, Now);

        var failure = Assert.Single(failures);
        Assert.Equal("has an RSA key of 1024 bits; at least 2048 bits are required.", failure);
    }

    [Fact]
    public void FindFailures_WhenTheCertificateIsSignedWithSha1_ReportsTheSignatureAlgorithm()
    {
        using var certificate = TransportSecurityTestCertificates.CreateRsa(
            2048,
            HashAlgorithmName.SHA1,
            Now
        );

        var failures = TransportCertificateInspector.FindFailures(certificate, Now);

        var failure = Assert.Single(failures);
        Assert.StartsWith("is signed with ", failure, StringComparison.Ordinal);
        Assert.EndsWith(
            "only SHA-256 or stronger with RSA or ECDSA is accepted.",
            failure,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void FindFailures_WhenTheDevelopmentMarkerIsPresent_ReportsTheDevelopmentCertificate()
    {
        using var certificate = TransportSecurityTestCertificates.CreateDevelopmentMarked(Now);

        var failures = TransportCertificateInspector.FindFailures(certificate, Now);

        var failure = Assert.Single(failures);
        Assert.StartsWith(
            "is the ASP.NET Core HTTPS development certificate",
            failure,
            StringComparison.Ordinal
        );
    }

    [Fact]
    public void FindFailures_WhenSeveralRulesBreak_ReportsEachOnce()
    {
        using var certificate = TransportSecurityTestCertificates.CreateDevelopmentMarked(
            Now.AddDays(-100)
        );

        var failures = TransportCertificateInspector.FindFailures(certificate, Now);

        Assert.Equal(2, failures.Count);
        Assert.StartsWith(
            "is the ASP.NET Core HTTPS development certificate",
            failures[0],
            StringComparison.Ordinal
        );
        Assert.StartsWith("expired at ", failures[1], StringComparison.Ordinal);
    }

    [Fact]
    public void FindFailures_WhenTheCertificateIsSignedWithPssAndSha256_ReturnsNone()
    {
        using var certificate = TransportSecurityTestCertificates.CreateRsa(
            2048,
            HashAlgorithmName.SHA256,
            Now,
            RSASignaturePadding.Pss
        );

        Assert.Empty(TransportCertificateInspector.FindFailures(certificate, Now));
    }

    #endregion

    #region IsStrongPssHash Tests

    [Fact]
    public void IsStrongPssHash_WhenTheHashIsSha256_ReturnsTrue()
    {
        var parameters = CreatePssParameters("2.16.840.1.101.3.4.2.1");

        Assert.True(TransportCertificateInspector.IsStrongPssHash(parameters));
    }

    [Fact]
    public void IsStrongPssHash_WhenTheHashIsSha384_ReturnsTrue()
    {
        var parameters = CreatePssParameters("2.16.840.1.101.3.4.2.2");

        Assert.True(TransportCertificateInspector.IsStrongPssHash(parameters));
    }

    [Fact]
    public void IsStrongPssHash_WhenTheHashIsSha1_ReturnsFalse()
    {
        var parameters = CreatePssParameters("1.3.14.3.2.26");

        Assert.False(TransportCertificateInspector.IsStrongPssHash(parameters));
    }

    [Fact]
    public void IsStrongPssHash_WhenTheParametersAreAbsent_ReturnsFalse()
    {
        Assert.False(TransportCertificateInspector.IsStrongPssHash(ReadOnlyMemory<byte>.Empty));
    }

    [Fact]
    public void IsStrongPssHash_WhenTheParametersHoldNoHash_ReturnsFalse()
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence()) { }

        Assert.False(TransportCertificateInspector.IsStrongPssHash(writer.Encode()));
    }

    [Fact]
    public void IsStrongPssHash_WhenTheParametersAreMalformed_ReturnsFalse()
    {
        byte[] parameters = [0x01, 0x02];

        Assert.False(TransportCertificateInspector.IsStrongPssHash(parameters));
    }

    #endregion

    #region Helpers

    // RSASSA-PSS-params with only the hash algorithm: SEQUENCE { [0] { SEQUENCE { OID hash } } }.
    private static byte[] CreatePssParameters(string hashOid)
    {
        var writer = new AsnWriter(AsnEncodingRules.DER);
        using (writer.PushSequence())
        {
            using (
                writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, isConstructed: true))
            )
            {
                using (writer.PushSequence())
                {
                    writer.WriteObjectIdentifier(hashOid);
                }
            }
        }

        return writer.Encode();
    }

    #endregion
}
