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
