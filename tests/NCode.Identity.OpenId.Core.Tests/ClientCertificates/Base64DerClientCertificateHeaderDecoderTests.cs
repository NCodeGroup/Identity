// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using NCode.Identity.OpenId.Authentication.ClientCertificates;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class Base64DerClientCertificateHeaderDecoderTests
{
    private Base64DerClientCertificateHeaderDecoder Decoder { get; } = new();

    #region Format Property Tests

    [Fact]
    public void Format_ReturnsBase64Der()
    {
        Assert.Equal(ClientCertificateHeaderFormats.Base64Der, Decoder.Format);
    }

    #endregion

    #region TryDecode Tests

    [Fact]
    public void TryDecode_WhenSingleCertificate_ReturnsItWithoutIntermediates()
    {
        using var certificate = TestCertificates.CreateSelfSigned("CN=client");
        var headerValue = TestCertificates.ToBase64Der(certificate);

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.True(decoded);
        Assert.NotNull(presented);
        Assert.Equal(certificate.RawData, presented.Certificate.RawData);
        Assert.Empty(presented.Intermediates);
    }

    [Fact]
    public void TryDecode_WhenCommaSeparatedChain_ReturnsLeafAndIntermediatesInOrder()
    {
        using var root = TestCertificates.CreateRootAuthority("Root CA");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, "Issuing CA");
        using var leaf = TestCertificates.CreateClientCertificate(intermediate, "CN=client");
        var leafDer = TestCertificates.ToBase64Der(leaf);
        var intermediateDer = TestCertificates.ToBase64Der(intermediate);
        var rootDer = TestCertificates.ToBase64Der(root);
        var headerValue = $"{leafDer}, {intermediateDer}, {rootDer}";

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.True(decoded);
        Assert.NotNull(presented);
        Assert.Equal(leaf.RawData, presented.Certificate.RawData);
        Assert.Equal(2, presented.Intermediates.Length);
        Assert.Equal(intermediate.RawData, presented.Intermediates[0].RawData);
        Assert.Equal(root.RawData, presented.Intermediates[1].RawData);
    }

    [Theory]
    [InlineData("not base64!")]
    [InlineData("AAAA")]
    [InlineData(",")]
    public void TryDecode_WhenNotCertificates_ReturnsFalse(string headerValue)
    {
        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.False(decoded);
        Assert.Null(presented);
    }

    [Fact]
    public void TryDecode_WhenAnyListEntryIsEmpty_ReturnsFalse()
    {
        using var certificate = TestCertificates.CreateSelfSigned("CN=client");
        var base64Der = TestCertificates.ToBase64Der(certificate);

        var decoded = Decoder.TryDecode($"{base64Der},,", out var presented);

        Assert.False(decoded);
        Assert.Null(presented);
    }

    #endregion
}
