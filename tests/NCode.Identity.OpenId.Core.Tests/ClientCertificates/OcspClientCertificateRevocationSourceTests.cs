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

using System.Net;
using System.Numerics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public class OcspClientCertificateRevocationSourceTests : BaseTests
{
    #region Scaffold

    private const string OcspUri = "http://ocsp.example.test/ocsp";
    private const string RootCrlUri = "http://crl.example.test/root.crl";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private MemoryCache MemoryCache { get; } = new(new MemoryCacheOptions());

    public OcspClientCertificateRevocationSourceTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            MemoryCache.Dispose();
        }

        base.Dispose(disposing);
    }

    private OcspClientCertificateRevocationSource CreateSource(HttpMessageHandler? handler = null)
    {
        if (handler is not null)
        {
            MockHttpClientFactory
                .Setup(x => x.CreateClient(RevocationDataFetcher.HttpClientName))
                .Returns(() => new HttpClient(handler, disposeHandler: false))
                .Verifiable();
        }

        var effective = Options.Create(new ClientCertificateRevocationOptions());
        var fetcher = new RevocationDataFetcher(
            MockHttpClientFactory.Object,
            new DefaultOutboundHttpContentReader(),
            effective,
            CreateLooseMock<ILogger<RevocationDataFetcher>>().Object
        );
        var cache = new RevocationRetrievalCache(
            MemoryCache,
            effective,
            CreateLooseMock<ILogger<RevocationRetrievalCache>>().Object
        );
        var crlSource = new CrlClientCertificateRevocationSource(
            TimeProvider.System,
            fetcher,
            cache,
            effective,
            CreateLooseMock<ILogger<CrlClientCertificateRevocationSource>>().Object
        );
        return new OcspClientCertificateRevocationSource(
            TimeProvider.System,
            fetcher,
            cache,
            crlSource,
            effective,
            CreateLooseMock<ILogger<OcspClientCertificateRevocationSource>>().Object
        );
    }

    private static X509Certificate2 CreateLeaf(X509Certificate2 root, string ocspUri = OcspUri) =>
        TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { OcspUris = [ocspUri] }
        );

    private static byte[] BuildRootCrl(X509Certificate2 root, params X509Certificate2[] revoked)
    {
        var builder = new CertificateRevocationListBuilder();
        foreach (var certificate in revoked)
            builder.AddEntry(certificate);

        return builder.Build(
            root,
            BigInteger.One,
            DateTimeOffset.UtcNow.AddDays(1),
            HashAlgorithmName.SHA256
        );
    }

    private static ClientCertificateRevocationContext Context(
        X509Certificate2 certificate,
        X509Certificate2 root
    ) =>
        new()
        {
            Certificate = certificate,
            Issuer = root,
            TrustAnchor = root,
        };

    private sealed record RecordedRequest(HttpMethod Method, string Uri, string? ContentType);

    private sealed class StubHttpMessageHandler(
        string ocspUri,
        byte[]? ocspResponse,
        byte[]? rootCrl = null
    ) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        public int RequestCount => Requests.Count;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var uri = request.RequestUri!.AbsoluteUri;
            Requests.Add(
                new RecordedRequest(
                    request.Method,
                    uri,
                    request.Content?.Headers.ContentType?.MediaType
                )
            );

            var body =
                uri.StartsWith(ocspUri, StringComparison.Ordinal) ? ocspResponse
                : string.Equals(uri, RootCrlUri, StringComparison.Ordinal) ? rootCrl
                : null;
            var response = body is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(body),
                };
            return Task.FromResult(response);
        }
    }

    #endregion

    #region Name Tests

    [Fact]
    public void Name_IsOcsp()
    {
        var source = CreateSource();

        Assert.Equal(ClientCertificateRevocationSourceNames.Ocsp, source.Name);
    }

    #endregion

    #region CheckAsync Tests

    [Fact]
    public async Task CheckAsync_WithoutAnOcspLocation_ReturnsUnknownWithoutARequest()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var source = CreateSource();

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
    }

    [Fact]
    public async Task CheckAsync_WhenTheIssuerAnswersGood_ReturnsGood()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(
            OcspUri,
            TestOcspResponses.Build(leaf, root, root)
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);
        Assert.StartsWith(OcspUri + "/", request.Uri, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckAsync_WhenTheIssuerAnswersRevoked_ReturnsRevoked()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(
            OcspUri,
            TestOcspResponses.Build(leaf, root, root, ClientCertificateRevocationStatus.Revoked)
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheResponderAnswersUnknown_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(
            OcspUri,
            TestOcspResponses.Build(leaf, root, root, ClientCertificateRevocationStatus.Unknown)
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenAnotherAuthoritySignsTheResponse_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var otherRoot = TestCertificates.CreateRootAuthority("Other Root");
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(
            OcspUri,
            TestOcspResponses.Build(leaf, root, otherRoot)
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenADelegatedResponderWithNoCheckAnswers_ReturnsItsStatus()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var responder = TestCertificates.CreateOcspResponder(
            root,
            "Responder",
            noCheck: true
        );
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(
            OcspUri,
            TestOcspResponses.Build(
                leaf,
                root,
                responder,
                ClientCertificateRevocationStatus.Revoked,
                certificates: [responder]
            )
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenADelegatedResponderWithoutNoCheckIsUnrevoked_ReturnsItsStatus()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var responder = TestCertificates.CreateOcspResponder(
            root,
            "Responder",
            noCheck: false,
            [RootCrlUri]
        );
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(
            OcspUri,
            TestOcspResponses.Build(
                leaf,
                root,
                responder,
                ClientCertificateRevocationStatus.Revoked,
                certificates: [responder]
            ),
            BuildRootCrl(root)
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenADelegatedResponderWithoutNoCheckIsRevoked_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var responder = TestCertificates.CreateOcspResponder(
            root,
            "Responder",
            noCheck: false,
            [RootCrlUri]
        );
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(
            OcspUri,
            TestOcspResponses.Build(
                leaf,
                root,
                responder,
                ClientCertificateRevocationStatus.Good,
                certificates: [responder]
            ),
            BuildRootCrl(root, responder)
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheResponseIsStale_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(
            OcspUri,
            TestOcspResponses.Build(
                leaf,
                root,
                root,
                thisUpdate: DateTimeOffset.UtcNow.AddDays(-2),
                nextUpdate: DateTimeOffset.UtcNow.AddDays(-1)
            )
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheResponseHasNoNextUpdateAndIsOld_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(
            OcspUri,
            TestOcspResponses.Build(
                leaf,
                root,
                root,
                thisUpdate: DateTimeOffset.UtcNow.AddHours(-1),
                omitNextUpdate: true
            )
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheResponseHasNoNextUpdate_AsksAgainNextTime()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(
            OcspUri,
            TestOcspResponses.Build(leaf, root, root, omitNextUpdate: true)
        );
        var source = CreateSource(handler);

        var first = await source.CheckAsync(Context(leaf, root), CancellationToken.None);
        var second = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, first);
        Assert.Equal(ClientCertificateRevocationStatus.Good, second);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_CalledTwice_AsksTheResponderOnce()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(
            OcspUri,
            TestOcspResponses.Build(leaf, root, root)
        );
        var source = CreateSource(handler);

        var first = await source.CheckAsync(Context(leaf, root), CancellationToken.None);
        var second = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, first);
        Assert.Equal(ClientCertificateRevocationStatus.Good, second);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheResponderIsUnreachable_RemembersTheLocation()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var firstLeaf = CreateLeaf(root);
        using var secondLeaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(OcspUri, null);
        var source = CreateSource(handler);

        var first = await source.CheckAsync(Context(firstLeaf, root), CancellationToken.None);
        var second = await source.CheckAsync(Context(secondLeaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, first);
        Assert.Equal(ClientCertificateRevocationStatus.Unknown, second);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheResponderAnswersTryLater_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(OcspUri, TestOcspResponses.Unsuccessful(3));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheResponseAnswersForAnotherCertificate_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeaf(root);
        var handler = new StubHttpMessageHandler(
            OcspUri,
            TestOcspResponses.Build(
                leaf,
                root,
                root,
                serialNumber: TestCertificates.CreateSerialNumber()
            )
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheRequestIsTooLongForGet_PostsIt()
    {
        var longUri = "http://ocsp.example.test/" + new string('a', 240);
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeaf(root, longUri);
        var handler = new StubHttpMessageHandler(
            longUri,
            TestOcspResponses.Build(leaf, root, root)
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, result);
        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(longUri, request.Uri);
        Assert.Equal("application/ocsp-request", request.ContentType);
    }

    #endregion
}
