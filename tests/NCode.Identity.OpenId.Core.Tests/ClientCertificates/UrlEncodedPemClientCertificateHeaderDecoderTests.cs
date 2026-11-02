// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class UrlEncodedPemClientCertificateHeaderDecoderTests
{
    private UrlEncodedPemClientCertificateHeaderDecoder Decoder { get; } = new();

    #region Format Property Tests

    [Fact]
    public void Format_ReturnsUrlEncodedPem()
    {
        Assert.Equal(ClientCertificateHeaderFormats.UrlEncodedPem, Decoder.Format);
    }

    #endregion

    #region TryDecode Tests

    [Fact]
    public void TryDecode_WhenUrlEncodedChain_ReturnsLeafAndIntermediates()
    {
        using var root = TestCertificates.CreateRootAuthority("Root CA");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, "Issuing CA");
        using var leaf = TestCertificates.CreateClientCertificate(intermediate, "CN=client");
        var leafPem = leaf.ExportCertificatePem();
        var intermediatePem = intermediate.ExportCertificatePem();
        var headerValue = Uri.EscapeDataString($"{leafPem}\n{intermediatePem}");

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.True(decoded);
        Assert.NotNull(presented);
        Assert.Equal(leaf.RawData, presented.Certificate.RawData);
        var single = Assert.Single(presented.Intermediates);
        Assert.Equal(intermediate.RawData, single.RawData);
    }

    [Fact]
    public void TryDecode_WhenNotPem_ReturnsFalse()
    {
        var decoded = Decoder.TryDecode("MIIB%2FAAAA", out var presented);

        Assert.False(decoded);
        Assert.Null(presented);
    }

    #endregion
}
