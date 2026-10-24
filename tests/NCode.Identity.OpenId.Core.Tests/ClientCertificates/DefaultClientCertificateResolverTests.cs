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
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public sealed class DefaultClientCertificateResolverTests : BaseTests
{
    private const string HeaderName = "X-Forwarded-Tls-Client-Cert";

    private DefaultHttpContext HttpContext { get; } = new();

    private Mock<OpenIdContext> MockContext { get; }

    public DefaultClientCertificateResolverTests()
    {
        MockContext = CreateStrictMock<OpenIdContext>();
    }

    private OpenIdContext CreateContext()
    {
        MockContext.SetupGet(x => x.Http).Returns(HttpContext).Verifiable();
        return MockContext.Object;
    }

    private DefaultClientCertificateResolver CreateResolver(
        string? headerName,
        string format = ClientCertificateHeaderFormats.Base64Der
    )
    {
        var options = new MutualTlsForwardingOptions
        {
            CertificateHeaderName = headerName,
            Format = format,
        };
        IClientCertificateHeaderDecoder[] decoders =
        [
            new Base64DerClientCertificateHeaderDecoder(),
            new PemClientCertificateHeaderDecoder(),
        ];
        var logger = CreateLooseMock<ILogger<DefaultClientCertificateResolver>>().Object;
        return new DefaultClientCertificateResolver(Options.Create(options), decoders, logger);
    }

    private void SetupErrorFactory()
    {
        var mockErrorFactory = CreateLooseMock<IOpenIdErrorFactory>();
        mockErrorFactory
            .Setup(x => x.Create(It.IsAny<string>()))
            .Returns<string>(code =>
            {
                var mockError = CreateLooseMock<IOpenIdError>();
                mockError.SetupAllProperties();
                mockError.Object.Code = code;
                return mockError.Object;
            });
        MockContext.SetupGet(x => x.ErrorFactory).Returns(mockErrorFactory.Object).Verifiable();
    }

    private void SetupConnectionCertificate(X509Certificate2 certificate)
    {
        var mockTlsFeature = CreateStrictMock<ITlsConnectionFeature>();
        mockTlsFeature.SetupGet(x => x.ClientCertificate).Returns(certificate).Verifiable();
        HttpContext.Features.Set(mockTlsFeature.Object);
    }

    #region CreateDecoderCatalog Tests

    [Fact]
    public void CreateDecoderCatalog_WhenTwoDecodersShareFormat_Throws()
    {
        IClientCertificateHeaderDecoder[] decoders =
        [
            new Base64DerClientCertificateHeaderDecoder(),
            new Base64DerClientCertificateHeaderDecoder(),
        ];

        var exception = Assert.Throws<InvalidOperationException>(() =>
            DefaultClientCertificateResolver.CreateDecoderCatalog(decoders)
        );

        Assert.Contains("'Base64Der'", exception.Message, StringComparison.Ordinal);
    }

    #endregion

    #region ResolveCertificateAsync Tests

    [Fact]
    public async Task ResolveCertificateAsync_WhenForwardedChain_ReturnsLeafAndIntermediates()
    {
        using var root = TestCertificates.CreateRootAuthority("Root CA");
        using var intermediate = TestCertificates.CreateIntermediateAuthority(root, "Issuing CA");
        using var leaf = TestCertificates.CreateClientCertificate(intermediate, "CN=client");
        var leafDer = TestCertificates.ToBase64Der(leaf);
        var intermediateDer = TestCertificates.ToBase64Der(intermediate);
        HttpContext.Request.Headers[HeaderName] = $"{leafDer},{intermediateDer}";
        var context = CreateContext();
        var resolver = CreateResolver(HeaderName);

        var resolved = await resolver.ResolveCertificateAsync(context, CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(leaf.RawData, resolved.Certificate.RawData);
        var single = Assert.Single(resolved.Intermediates);
        Assert.Equal(intermediate.RawData, single.RawData);
    }

    [Fact]
    public async Task ResolveCertificateAsync_WhenFormatIsPem_UsesItsDecoder()
    {
        using var certificate = TestCertificates.CreateSelfSigned("CN=client");
        HttpContext.Request.Headers[HeaderName] = certificate.ExportCertificatePem();
        var context = CreateContext();
        var resolver = CreateResolver(HeaderName, ClientCertificateHeaderFormats.Pem);

        var resolved = await resolver.ResolveCertificateAsync(context, CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(certificate.RawData, resolved.Certificate.RawData);
    }

    [Fact]
    public async Task ResolveCertificateAsync_WhenHeaderConfiguredButAbsent_IgnoresConnectionCertificate()
    {
        using var proxyCertificate = TestCertificates.CreateSelfSigned("CN=proxy");
        HttpContext.Connection.ClientCertificate = proxyCertificate;
        var context = CreateContext();
        var resolver = CreateResolver(HeaderName);

        var resolved = await resolver.ResolveCertificateAsync(context, CancellationToken.None);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveCertificateAsync_WhenForwardedValueMalformed_ThrowsInvalidRequest()
    {
        SetupErrorFactory();
        HttpContext.Request.Headers[HeaderName] = "not-a-certificate";
        var context = CreateContext();
        var resolver = CreateResolver(HeaderName);

        var exception = await Assert.ThrowsAsync<OpenIdException>(() =>
            resolver.ResolveCertificateAsync(context, CancellationToken.None).AsTask()
        );

        Assert.Equal(OpenIdConstants.ErrorCodes.InvalidRequest, exception.Error.Code);
        Assert.Equal(StatusCodes.Status400BadRequest, exception.Error.StatusCode);
    }

    [Fact]
    public async Task ResolveCertificateAsync_WhenFormatNotRegistered_Throws()
    {
        HttpContext.Request.Headers[HeaderName] = "value";
        var context = CreateContext();
        var resolver = CreateResolver(HeaderName, "Unregistered");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            resolver.ResolveCertificateAsync(context, CancellationToken.None).AsTask()
        );
    }

    [Fact]
    public async Task ResolveCertificateAsync_WhenNoHeaderConfigured_ReturnsConnectionCertificate()
    {
        using var certificate = TestCertificates.CreateSelfSigned("CN=client");
        SetupConnectionCertificate(certificate);
        var context = CreateContext();
        var resolver = CreateResolver(headerName: null);

        var resolved = await resolver.ResolveCertificateAsync(context, CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Same(certificate, resolved.Certificate);
        Assert.Empty(resolved.Intermediates);
    }

    [Fact]
    public async Task ResolveCertificateAsync_WhenNoHeaderConfiguredAndNoCertificate_ReturnsNull()
    {
        var context = CreateContext();
        var resolver = CreateResolver(headerName: null);

        var resolved = await resolver.ResolveCertificateAsync(context, CancellationToken.None);

        Assert.Null(resolved);
    }

    [Fact]
    public async Task ResolveCertificateAsync_WhenCalledTwice_ResolvesOncePerRequest()
    {
        using var certificate = TestCertificates.CreateSelfSigned("CN=client");
        HttpContext.Request.Headers[HeaderName] = TestCertificates.ToBase64Der(certificate);
        var context = CreateContext();
        var resolver = CreateResolver(HeaderName);

        var first = await resolver.ResolveCertificateAsync(context, CancellationToken.None);
        HttpContext.Request.Headers.Remove(HeaderName);
        var second = await resolver.ResolveCertificateAsync(context, CancellationToken.None);

        Assert.NotNull(first);
        Assert.Same(first, second);
    }

    #endregion
}
