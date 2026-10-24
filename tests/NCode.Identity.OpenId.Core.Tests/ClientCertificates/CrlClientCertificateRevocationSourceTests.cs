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

public class CrlClientCertificateRevocationSourceTests : BaseTests
{
    #region Scaffold

    private const string CrlUri = "http://crl.example.test/root.crl";
    private const string IssuingCrlUri = "http://crl.example.test/issuing.crl";
    private const string RootCrlUri = "http://crl.example.test/root.crl";
    private const string IndirectCrlUri = "http://crl.example.test/indirect.crl";
    private const string DeltaCrlUri = "http://crl.example.test/delta.crl";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private MemoryCache MemoryCache { get; } = new(new MemoryCacheOptions());

    public CrlClientCertificateRevocationSourceTests()
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

    private CrlClientCertificateRevocationSource CreateSource(
        HttpMessageHandler? handler = null,
        ClientCertificateRevocationOptions? options = null
    )
    {
        if (handler is not null)
        {
            MockHttpClientFactory
                .Setup(x => x.CreateClient(RevocationDataFetcher.HttpClientName))
                .Returns(() => new HttpClient(handler, disposeHandler: false))
                .Verifiable();
        }

        var effective = Options.Create(options ?? new ClientCertificateRevocationOptions());
        return new CrlClientCertificateRevocationSource(
            TimeProvider.System,
            new RevocationDataFetcher(
                MockHttpClientFactory.Object,
                new DefaultOutboundHttpContentReader(),
                effective,
                CreateLooseMock<ILogger<RevocationDataFetcher>>().Object
            ),
            new RevocationRetrievalCache(
                MemoryCache,
                effective,
                CreateLooseMock<ILogger<RevocationRetrievalCache>>().Object
            ),
            effective,
            CreateLooseMock<ILogger<CrlClientCertificateRevocationSource>>().Object
        );
    }

    private static byte[] BuildCrl(X509Certificate2 root, params X509Certificate2[] revoked)
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

    private static HttpResponseMessage CrlResponse(byte[] crl) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(crl) };

    private static HttpResponseMessage NotFoundResponse() => new(HttpStatusCode.NotFound);

    private static StubHttpMessageHandler RespondByUri(params (string Uri, byte[] Crl)[] responses)
    {
        var byUri = responses.ToDictionary(
            response => response.Uri,
            response => response.Crl,
            StringComparer.Ordinal
        );
        return new StubHttpMessageHandler(request =>
            byUri.TryGetValue(request.RequestUri!.AbsoluteUri, out var crl)
                ? CrlResponse(crl)
                : NotFoundResponse()
        );
    }

    private static X509Extension Partitions(string firstUri, string secondUri) =>
        TestCrls.DistributionPoints(
            TestCrls.CrlDistributionPointsOid,
            new TestDistributionPoint(firstUri, 0x006),
            new TestDistributionPoint(secondUri, 0x1F8)
        );

    private static byte[] BuildPartitionCrl(
        X509Certificate2 root,
        string uri,
        int reasons,
        X509Certificate2? revoked = null
    ) =>
        TestCrls.Build(
            root,
            revokedSerialNumbers: revoked is null ? null : [revoked.SerialNumberBytes.ToArray()],
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(uri: uri, onlySomeReasons: reasons)
                ),
            ]
        );

    private static X509Certificate2 CreateLeafWithFreshestCrl(X509Certificate2 root) =>
        TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions
            {
                CrlDistributionPoints = [CrlUri],
                ExtraExtensions =
                [
                    TestCrls.DistributionPoints(
                        TestCrls.FreshestCrlOid,
                        new TestDistributionPoint(DeltaCrlUri)
                    ),
                ],
            }
        );

    private static byte[] BuildNumberedCrl(
        X509Certificate2 root,
        int number,
        IReadOnlyList<TestCrlEntry>? entries = null,
        IReadOnlyList<(string Oid, bool Critical, byte[] Value)>? extraExtensions = null
    ) =>
        TestCrls.Build(
            root,
            extensions:
            [
                (TestCrls.CrlNumberOid, false, TestCrls.CrlNumber(number)),
                .. extraExtensions ?? [],
            ],
            entries: entries
        );

    private static byte[] BuildDeltaCrl(
        X509Certificate2 root,
        int number,
        int baseNumber,
        IReadOnlyList<TestCrlEntry>? entries = null
    ) =>
        BuildNumberedCrl(
            root,
            number,
            entries,
            [(TestCrls.DeltaCrlIndicatorOid, true, TestCrls.DeltaCrlIndicator(baseNumber))]
        );

    private static TestCrlEntry Entry(X509Certificate2 certificate, int? reasonCode = null) =>
        new(
            certificate.SerialNumberBytes.ToArray(),
            reasonCode is { } code
                ? [(TestCrls.ReasonCodeOid, false, TestCrls.ReasonCode(code))]
                : null
        );

    private static ClientCertificateRevocationContext Context(
        X509Certificate2 certificate,
        X509Certificate2 issuer
    ) =>
        new()
        {
            Certificate = certificate,
            Issuer = issuer,
            TrustAnchor = issuer,
        };

    private static ClientCertificateRevocationContext Context(
        X509Certificate2 certificate,
        X509Certificate2 issuer,
        X509Certificate2 trustAnchor,
        params X509Certificate2[] intermediates
    ) =>
        new()
        {
            Certificate = certificate,
            Issuer = issuer,
            TrustAnchor = trustAnchor,
            Intermediates = intermediates,
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, HttpResponseMessage> respond
    ) : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestCount++;
            return Task.FromResult(respond(request));
        }
    }

    private sealed class AsyncStubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond
    ) : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            Interlocked.Increment(ref _requestCount);
            return respond(request, cancellationToken);
        }
    }

    #endregion

    #region Name Tests

    [Fact]
    public void Name_IsCrl()
    {
        var source = CreateSource();

        Assert.Equal(ClientCertificateRevocationSourceNames.Crl, source.Name);
    }

    #endregion

    #region CheckAsync Tests

    [Fact]
    public async Task CheckAsync_WhenTheCrlListsTheCertificate_ReturnsRevoked()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var crl = BuildCrl(root, leaf);
        var handler = new StubHttpMessageHandler(_ => CrlResponse(crl));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheCrlDoesNotListIt_ReturnsGood()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        using var other = TestCertificates.CreateClientCertificate(root, "CN=other");
        var crl = BuildCrl(root, other);
        var handler = new StubHttpMessageHandler(_ => CrlResponse(crl));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WithoutADistributionPoint_ReturnsUnknownWithoutFetching()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var source = CreateSource();

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
    }

    [Fact]
    public async Task CheckAsync_WithOnlyAnLdapDistributionPoint_ReturnsUnknownWithoutFetching()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions
            {
                CrlDistributionPoints = ["ldap://crl.example.test/root"],
            }
        );
        var source = CreateSource();

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
    }

    [Fact]
    public async Task CheckAsync_WhenTheFetchFails_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var handler = new StubHttpMessageHandler(_ => NotFoundResponse());
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenAnotherAuthoritySignedTheCrl_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var otherRoot = TestCertificates.CreateRootAuthority("Other Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var crl = BuildCrl(otherRoot);
        var handler = new StubHttpMessageHandler(_ => CrlResponse(crl));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheCrlIsStale_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var crl = TestCrls.Build(
            root,
            thisUpdate: DateTimeOffset.UtcNow.AddDays(-2),
            nextUpdate: DateTimeOffset.UtcNow.AddDays(-1)
        );
        var handler = new StubHttpMessageHandler(_ => CrlResponse(crl));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheCrlIsForAnotherDistributionPoint_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var crl = TestCrls.Build(
            root,
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint("http://crl.example.test/other.crl")
                ),
            ]
        );
        var handler = new StubHttpMessageHandler(_ => CrlResponse(crl));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheFirstDistributionPointFails_UsesTheNext()
    {
        const string firstUri = "http://crl.example.test/first.crl";
        const string secondUri = "http://crl.example.test/second.crl";
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [firstUri, secondUri] }
        );
        var crl = BuildCrl(root, leaf);
        var handler = new StubHttpMessageHandler(request =>
            request.RequestUri?.AbsolutePath == "/first.crl" ? NotFoundResponse() : CrlResponse(crl)
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_CalledTwice_FetchesTheCrlOnce()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var crl = BuildCrl(root, leaf);
        var handler = new StubHttpMessageHandler(_ => CrlResponse(crl));
        var source = CreateSource(handler);

        var first = await source.CheckAsync(Context(leaf, root), CancellationToken.None);
        var second = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, first);
        Assert.Equal(ClientCertificateRevocationStatus.Revoked, second);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenAnotherIssuerNamesTheSameUri_DoesNotTrustTheCachedCrl()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var otherRoot = TestCertificates.CreateRootAuthority("Other Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        using var otherLeaf = TestCertificates.CreateClientCertificate(
            otherRoot,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var crl = BuildCrl(root);
        var handler = new StubHttpMessageHandler(_ => CrlResponse(crl));
        var source = CreateSource(handler);

        var first = await source.CheckAsync(Context(leaf, root), CancellationToken.None);
        var second = await source.CheckAsync(Context(otherLeaf, otherRoot), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, first);
        Assert.Equal(ClientCertificateRevocationStatus.Unknown, second);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheFetchFailedRecently_DoesNotFetchAgain()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var handler = new StubHttpMessageHandler(_ => NotFoundResponse());
        var source = CreateSource(handler);

        var first = await source.CheckAsync(Context(leaf, root), CancellationToken.None);
        var second = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, first);
        Assert.Equal(ClientCertificateRevocationStatus.Unknown, second);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheListExceedsTheConfiguredSize_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var crl = BuildCrl(root, leaf);
        var handler = new StubHttpMessageHandler(_ => CrlResponse(crl));
        var source = CreateSource(
            handler,
            new ClientCertificateRevocationOptions { MaxResponseSizeBytes = 16 }
        );

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheFetchOutlastsTheConfiguredTimeout_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var handler = new AsyncStubHttpMessageHandler(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return NotFoundResponse();
            }
        );
        var source = CreateSource(
            handler,
            new ClientCertificateRevocationOptions { FetchTimeout = TimeSpan.FromMilliseconds(100) }
        );

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTwoChecksRunAtOnce_RetrievesTheListOnce()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var crl = BuildCrl(root);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new AsyncStubHttpMessageHandler(
            async (_, _) =>
            {
                await gate.Task;
                return CrlResponse(crl);
            }
        );
        var source = CreateSource(handler);

        var first = source.CheckAsync(Context(leaf, root), CancellationToken.None).AsTask();
        var second = source.CheckAsync(Context(leaf, root), CancellationToken.None).AsTask();
        gate.SetResult();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.Equal(ClientCertificateRevocationStatus.Good, result));
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheFirstCallerGivesUp_CompletesTheRetrievalForTheOthers()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var crl = BuildCrl(root);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var handler = new AsyncStubHttpMessageHandler(
            async (_, _) =>
            {
                await gate.Task;
                return CrlResponse(crl);
            }
        );
        var source = CreateSource(handler);
        using var cancellation = new CancellationTokenSource();
        var issuerCopy = X509CertificateLoader.LoadCertificate(root.RawData);

        var first = source.CheckAsync(Context(leaf, issuerCopy), cancellation.Token).AsTask();
        await cancellation.CancelAsync();
        issuerCopy.Dispose();
        var second = source.CheckAsync(Context(leaf, root), CancellationToken.None).AsTask();
        gate.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(ClientCertificateRevocationStatus.Good, await second);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheIssuersOtherKeySignsTheList_ReturnsTheStatus()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var issuing = TestCertificates.CreateIntermediateAuthority(root, "Issuing");
        using var rolled = TestCertificates.CreateIntermediateAuthority(
            root,
            "Issuing",
            [RootCrlUri]
        );
        using var leaf = TestCertificates.CreateClientCertificate(
            issuing,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [IssuingCrlUri] }
        );
        var handler = RespondByUri(
            (
                IssuingCrlUri,
                TestCrls.Build(rolled, revokedSerialNumbers: [leaf.SerialNumberBytes.ToArray()])
            ),
            (RootCrlUri, BuildCrl(root))
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(
            Context(leaf, issuing, root, rolled),
            CancellationToken.None
        );

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheOtherKeyIsNotConfigured_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var issuing = TestCertificates.CreateIntermediateAuthority(root, "Issuing");
        using var rolled = TestCertificates.CreateIntermediateAuthority(
            root,
            "Issuing",
            [RootCrlUri]
        );
        using var leaf = TestCertificates.CreateClientCertificate(
            issuing,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [IssuingCrlUri] }
        );
        var handler = RespondByUri(
            (
                IssuingCrlUri,
                TestCrls.Build(rolled, revokedSerialNumbers: [leaf.SerialNumberBytes.ToArray()])
            ),
            (RootCrlUri, BuildCrl(root))
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, issuing, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
    }

    [Fact]
    public async Task CheckAsync_WhenTheSignerChainsToAnotherAnchor_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var otherRoot = TestCertificates.CreateRootAuthority("Other Root");
        using var issuing = TestCertificates.CreateIntermediateAuthority(root, "Issuing");
        using var rolled = TestCertificates.CreateIntermediateAuthority(
            otherRoot,
            "Issuing",
            [RootCrlUri]
        );
        using var leaf = TestCertificates.CreateClientCertificate(
            issuing,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [IssuingCrlUri] }
        );
        var handler = RespondByUri(
            (
                IssuingCrlUri,
                TestCrls.Build(rolled, revokedSerialNumbers: [leaf.SerialNumberBytes.ToArray()])
            ),
            (RootCrlUri, BuildCrl(root))
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(
            Context(leaf, issuing, root, rolled),
            CancellationToken.None
        );

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
    }

    [Fact]
    public async Task CheckAsync_WhenTheSignerIsRevoked_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var issuing = TestCertificates.CreateIntermediateAuthority(root, "Issuing");
        using var rolled = TestCertificates.CreateIntermediateAuthority(
            root,
            "Issuing",
            [RootCrlUri]
        );
        using var leaf = TestCertificates.CreateClientCertificate(
            issuing,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [IssuingCrlUri] }
        );
        var handler = RespondByUri(
            (
                IssuingCrlUri,
                TestCrls.Build(rolled, revokedSerialNumbers: [leaf.SerialNumberBytes.ToArray()])
            ),
            (RootCrlUri, BuildCrl(root, rolled))
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(
            Context(leaf, issuing, root, rolled),
            CancellationToken.None
        );

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenAnIndirectListRevokesTheCertificate_ReturnsRevoked()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var signer = TestCertificates.CreateCrlSigner(root, "CRL Signer", [RootCrlUri]);
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions
            {
                ExtraExtensions =
                [
                    TestCrls.DistributionPoints(
                        TestCrls.CrlDistributionPointsOid,
                        new TestDistributionPoint(IndirectCrlUri, null, signer.SubjectName)
                    ),
                ],
            }
        );
        var indirect = TestCrls.Build(
            signer,
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(uri: IndirectCrlUri, indirect: true)
                ),
            ],
            entries:
            [
                new TestCrlEntry(
                    leaf.SerialNumberBytes.ToArray(),
                    [
                        (
                            TestCrls.CertificateIssuerOid,
                            true,
                            TestCrls.CertificateIssuer(root.SubjectName)
                        ),
                    ]
                ),
            ]
        );
        var handler = RespondByUri((IndirectCrlUri, indirect), (RootCrlUri, BuildCrl(root)));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(
            Context(leaf, root, root, signer),
            CancellationToken.None
        );

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
    }

    [Fact]
    public async Task CheckAsync_WhenAnIndirectListNamesTheSerialForAnotherIssuer_ReturnsGood()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var signer = TestCertificates.CreateCrlSigner(root, "CRL Signer", [RootCrlUri]);
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions
            {
                ExtraExtensions =
                [
                    TestCrls.DistributionPoints(
                        TestCrls.CrlDistributionPointsOid,
                        new TestDistributionPoint(IndirectCrlUri, null, signer.SubjectName)
                    ),
                ],
            }
        );
        var indirect = TestCrls.Build(
            signer,
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(uri: IndirectCrlUri, indirect: true)
                ),
            ],
            entries: [new TestCrlEntry(leaf.SerialNumberBytes.ToArray())]
        );
        var handler = RespondByUri((IndirectCrlUri, indirect), (RootCrlUri, BuildCrl(root)));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(
            Context(leaf, root, root, signer),
            CancellationToken.None
        );

        Assert.Equal(ClientCertificateRevocationStatus.Good, result);
    }

    [Fact]
    public async Task CheckAsync_WhenTheCheckIsNestedTooDeeply_ReturnsUnknownWithoutARequest()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var source = CreateSource();
        var context = new ClientCertificateRevocationContext
        {
            Certificate = leaf,
            Issuer = root,
            TrustAnchor = root,
            Depth = 3,
        };

        var result = await source.CheckAsync(context, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
    }

    [Fact]
    public async Task CheckAsync_WhenTheDeltaListRevokesTheCertificate_ReturnsRevoked()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeafWithFreshestCrl(root);
        var handler = RespondByUri(
            (CrlUri, BuildNumberedCrl(root, 10)),
            (DeltaCrlUri, BuildDeltaCrl(root, 11, 10, [Entry(leaf)]))
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheDeltaListDoesNotListIt_ReturnsGood()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeafWithFreshestCrl(root);
        var handler = RespondByUri(
            (CrlUri, BuildNumberedCrl(root, 10)),
            (DeltaCrlUri, BuildDeltaCrl(root, 11, 10))
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheDeltaListIsUnavailable_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeafWithFreshestCrl(root);
        var handler = RespondByUri((CrlUri, BuildNumberedCrl(root, 10)));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheDeltaListIsForALaterBase_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeafWithFreshestCrl(root);
        var handler = RespondByUri(
            (CrlUri, BuildNumberedCrl(root, 10)),
            (DeltaCrlUri, BuildDeltaCrl(root, 12, 11))
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheDeltaListRemovesTheCertificate_ReturnsGood()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = CreateLeafWithFreshestCrl(root);
        var handler = RespondByUri(
            (CrlUri, BuildNumberedCrl(root, 10, [Entry(leaf, 6)])),
            (DeltaCrlUri, BuildDeltaCrl(root, 11, 10, [Entry(leaf, 8)]))
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheCompleteListNamesTheDeltaLocation_UsesIt()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var handler = RespondByUri(
            (
                CrlUri,
                BuildNumberedCrl(
                    root,
                    10,
                    extraExtensions:
                    [
                        (
                            TestCrls.FreshestCrlOid,
                            false,
                            TestCrls
                                .DistributionPoints(
                                    TestCrls.FreshestCrlOid,
                                    new TestDistributionPoint(DeltaCrlUri)
                                )
                                .RawData
                        ),
                    ]
                )
            ),
            (DeltaCrlUri, BuildDeltaCrl(root, 11, 10, [Entry(leaf)]))
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTwoPartitionsCoverEveryReason_ReturnsGood()
    {
        const string firstUri = "http://crl.example.test/first.crl";
        const string secondUri = "http://crl.example.test/second.crl";
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { ExtraExtensions = [Partitions(firstUri, secondUri)] }
        );
        var handler = RespondByUri(
            (firstUri, BuildPartitionCrl(root, firstUri, 0x006)),
            (secondUri, BuildPartitionCrl(root, secondUri, 0x1F8))
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenAPartitionIsUnavailable_ReturnsUnknown()
    {
        const string firstUri = "http://crl.example.test/first.crl";
        const string secondUri = "http://crl.example.test/second.crl";
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { ExtraExtensions = [Partitions(firstUri, secondUri)] }
        );
        var handler = RespondByUri((firstUri, BuildPartitionCrl(root, firstUri, 0x006)));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenAPartitionListsTheCertificate_ReturnsRevoked()
    {
        const string firstUri = "http://crl.example.test/first.crl";
        const string secondUri = "http://crl.example.test/second.crl";
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { ExtraExtensions = [Partitions(firstUri, secondUri)] }
        );
        var handler = RespondByUri(
            (firstUri, BuildPartitionCrl(root, firstUri, 0x006)),
            (secondUri, BuildPartitionCrl(root, secondUri, 0x1F8, leaf))
        );
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, result);
        Assert.Equal(2, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheListCoversOnlySomeReasons_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var crl = TestCrls.Build(
            root,
            extensions:
            [
                (
                    TestCrls.IssuingDistributionPointOid,
                    true,
                    TestCrls.IssuingDistributionPoint(onlySomeReasons: 0x006)
                ),
            ]
        );
        var handler = RespondByUri((CrlUri, crl));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheEntryIsRemovedFromTheList_ReturnsGood()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var crl = TestCrls.Build(
            root,
            entries:
            [
                new TestCrlEntry(
                    leaf.SerialNumberBytes.ToArray(),
                    [(TestCrls.ReasonCodeOid, false, TestCrls.ReasonCode(8))]
                ),
            ]
        );
        var handler = RespondByUri((CrlUri, crl));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenTheListAtADistributionPointIsADeltaList_ReturnsUnknown()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(
            root,
            "CN=c",
            new ClientCertificateOptions { CrlDistributionPoints = [CrlUri] }
        );
        var crl = TestCrls.Build(
            root,
            extensions:
            [
                (TestCrls.CrlNumberOid, false, TestCrls.CrlNumber(11)),
                (TestCrls.DeltaCrlIndicatorOid, true, TestCrls.DeltaCrlIndicator(10)),
            ]
        );
        var handler = RespondByUri((CrlUri, crl));
        var source = CreateSource(handler);

        var result = await source.CheckAsync(Context(leaf, root), CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Unknown, result);
        Assert.Equal(1, handler.RequestCount);
    }

    #endregion
}
