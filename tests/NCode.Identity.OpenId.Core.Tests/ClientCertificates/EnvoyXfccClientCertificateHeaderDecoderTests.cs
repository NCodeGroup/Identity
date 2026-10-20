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

public sealed class EnvoyXfccClientCertificateHeaderDecoderTests : IDisposable
{
    private EnvoyXfccClientCertificateHeaderDecoder Decoder { get; } = new();

    private X509Certificate2 Root { get; } = TestCertificates.CreateRootAuthority("Root CA");

    private X509Certificate2 Intermediate { get; }

    private X509Certificate2 Leaf { get; }

    public EnvoyXfccClientCertificateHeaderDecoderTests()
    {
        Intermediate = TestCertificates.CreateIntermediateAuthority(Root, "Issuing CA");
        Leaf = TestCertificates.CreateClientCertificate(Intermediate, "CN=client");
    }

    public void Dispose()
    {
        Leaf.Dispose();
        Intermediate.Dispose();
        Root.Dispose();
    }

    private static string Encode(params X509Certificate2[] certificates)
    {
        var pem = string.Join(
            '\n',
            certificates.Select(certificate => certificate.ExportCertificatePem())
        );
        return Uri.EscapeDataString(pem);
    }

    #region Format Property Tests

    [Fact]
    public void Format_ReturnsEnvoyXfcc()
    {
        Assert.Equal(ClientCertificateHeaderFormats.EnvoyXfcc, Decoder.Format);
    }

    #endregion

    #region TryDecode Tests

    [Fact]
    public void TryDecode_WhenCertOnly_ReturnsCertificate()
    {
        var cert = Encode(Leaf);
        var headerValue =
            $"By=spiffe://cluster.local/ns/default/sa/server;Hash=abc;Cert=\"{cert}\"";

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.True(decoded);
        Assert.NotNull(presented);
        Assert.Equal(Leaf.RawData, presented.Certificate.RawData);
        Assert.Empty(presented.Intermediates);
    }

    [Fact]
    public void TryDecode_WhenChainAndMatchingCert_ReturnsLeafAndIntermediates()
    {
        var cert = Encode(Leaf);
        var chain = Encode(Leaf, Intermediate);
        var headerValue =
            $"Hash=abc;Cert=\"{cert}\";Chain=\"{chain}\";Subject=\"CN=client,O=Acme\"";

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.True(decoded);
        Assert.NotNull(presented);
        Assert.Equal(Leaf.RawData, presented.Certificate.RawData);
        var single = Assert.Single(presented.Intermediates);
        Assert.Equal(Intermediate.RawData, single.RawData);
    }

    [Fact]
    public void TryDecode_WhenCertDisagreesWithChain_ReturnsFalse()
    {
        var cert = Encode(Intermediate);
        var chain = Encode(Leaf, Intermediate);
        var headerValue = $"Cert=\"{cert}\";Chain=\"{chain}\"";

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.False(decoded);
        Assert.Null(presented);
    }

    [Fact]
    public void TryDecode_WhenSeveralElements_UsesTheFirst()
    {
        using var other = TestCertificates.CreateSelfSigned("CN=proxy");
        var cert = Encode(Leaf);
        var otherCert = Encode(other);
        var headerValue = $"Subject=\"CN=client,O=Acme\";Cert=\"{cert}\",Cert=\"{otherCert}\"";

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.True(decoded);
        Assert.NotNull(presented);
        Assert.Equal(Leaf.RawData, presented.Certificate.RawData);
    }

    [Fact]
    public void TryDecode_WhenQuotedValueHasEscapedQuote_ReadsIt()
    {
        var cert = Encode(Leaf);
        var headerValue = $"Subject=\"CN=\\\"client\\\"\";Cert={cert}";

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.True(decoded);
        Assert.NotNull(presented);
        Assert.Equal(Leaf.RawData, presented.Certificate.RawData);
    }

    [Theory]
    [InlineData("Hash=abc;Subject=\"CN=client\"")]
    [InlineData("Cert=\"unterminated")]
    [InlineData("Cert")]
    [InlineData("\"Cert\"=x")]
    [InlineData("Cert=not-a-certificate")]
    public void TryDecode_WhenNoDecodableCertificate_ReturnsFalse(string headerValue)
    {
        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.False(decoded);
        Assert.Null(presented);
    }

    [Fact]
    public void TryDecode_WhenCertRepeatedInElement_ReturnsFalse()
    {
        var cert = Encode(Leaf);
        var headerValue = $"Cert=\"{cert}\";cert=\"{cert}\"";

        var decoded = Decoder.TryDecode(headerValue, out var presented);

        Assert.False(decoded);
        Assert.Null(presented);
    }

    #endregion
}
