// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class PemClientCertificateHeaderDecoderTests
{
    private PemClientCertificateHeaderDecoder Decoder { get; } = new();

    #region Format Property Tests

    [Fact]
    public void Format_ReturnsPem()
    {
        Assert.Equal(ClientCertificateHeaderFormats.Pem, Decoder.Format);
    }

    #endregion

    #region TryDecode Tests

    [Fact]
    public void TryDecode_WhenSingleCertificate_ReturnsIt()
    {
        using var certificate = TestCertificates.CreateSelfSigned("CN=client");
        var headerValue = certificate.ExportCertificatePem();

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.True(decoded);
        Assert.NotNull(presented);
        Assert.Equal(certificate.RawData, presented.Certificate.RawData);
        Assert.Empty(presented.Intermediates);
    }

    [Fact]
    public void TryDecode_WhenChain_ReturnsLeafAndIntermediates()
    {
        using var root = TestCertificates.CreateRootAuthority("Root CA");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, "Issuing CA");
        using var leaf = TestCertificates.CreateClientCertificate(intermediate, "CN=client");
        var leafPem = leaf.ExportCertificatePem();
        var intermediatePem = intermediate.ExportCertificatePem();
        var headerValue = $"{leafPem}\n{intermediatePem}";

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.True(decoded);
        Assert.NotNull(presented);
        Assert.Equal(leaf.RawData, presented.Certificate.RawData);
        var single = Assert.Single(presented.Intermediates);
        Assert.Equal(intermediate.RawData, single.RawData);
    }

    [Fact]
    public void TryDecode_WhenTextPrecedesCertificate_ReturnsFalse()
    {
        using var certificate = TestCertificates.CreateSelfSigned("CN=client");
        var pem = certificate.ExportCertificatePem();

        var decoded = Decoder.TryDecode($"subject=CN=client\n{pem}", out var presented);

        Assert.False(decoded);
        Assert.Null(presented);
    }

    [Fact]
    public void TryDecode_WhenBlockIsNotCertificate_ReturnsFalse()
    {
        const string headerValue = "-----BEGIN PUBLIC KEY-----\nAAAA\n-----END PUBLIC KEY-----";

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.False(decoded);
        Assert.Null(presented);
    }

    [Fact]
    public void TryDecode_WhenCertificateIsCorrupt_ReturnsFalse()
    {
        const string headerValue = "-----BEGIN CERTIFICATE-----\nAAAA\n-----END CERTIFICATE-----";

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.False(decoded);
        Assert.Null(presented);
    }

    [Fact]
    public void TryDecode_WhenNoCertificate_ReturnsFalse()
    {
        var decoded = Decoder.TryDecode("   ", out var presented);

        Assert.False(decoded);
        Assert.Null(presented);
    }

    #endregion
}
