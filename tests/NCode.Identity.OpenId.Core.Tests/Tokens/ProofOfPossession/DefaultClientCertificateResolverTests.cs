#region Copyright Preamble

// Copyright @ 2024 NCode Group
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

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Tokens;
using NCode.Identity.OpenId.Authentication.Tokens.ProofOfPossession;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Tokens.ProofOfPossession;

public sealed class DefaultClientCertificateResolverTests
{
    private static DefaultClientCertificateResolver CreateResolver(
        MutualTlsForwardingOptions options
    ) => new(Options.Create(options));

    private static X509Certificate2 CreateCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=test-client",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1
        );
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1),
            DateTimeOffset.UtcNow.AddDays(1)
        );
    }

    #region ResolveCertificateAsync Tests

    [Fact]
    public async Task ResolveCertificateAsync_WhenBase64DerHeader_ReturnsForwardedCertificate()
    {
        using var certificate = CreateCertificate();
        const string headerName = "X-Client-Cert";
        var resolver = CreateResolver(
            new MutualTlsForwardingOptions
            {
                CertificateHeaderName = headerName,
                Format = MutualTlsClientCertificateFormat.Base64Der,
            }
        );

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Headers[headerName] = Convert.ToBase64String(certificate.RawData);

        var resolved = await resolver.ResolveCertificateAsync(httpContext, CancellationToken.None);

        Assert.NotNull(resolved);
        Assert.Equal(certificate.Thumbprint, resolved.Thumbprint);
    }

    [Fact]
    public async Task ResolveCertificateAsync_WhenNoHeaderConfigured_ReturnsConnectionCertificate()
    {
        var resolver = CreateResolver(new MutualTlsForwardingOptions());
        var httpContext = new DefaultHttpContext();

        var resolved = await resolver.ResolveCertificateAsync(httpContext, CancellationToken.None);

        Assert.Null(resolved);
    }

    #endregion
}
