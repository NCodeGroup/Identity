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

public class DefaultClientCertificateRevocationCheckerTests : BaseTests
{
    #region Scaffold

    private const string CrlUri = "http://crl.example.test/root.crl";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private MemoryCache MemoryCache { get; } = new(new MemoryCacheOptions());

    public DefaultClientCertificateRevocationCheckerTests()
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

    private DefaultClientCertificateRevocationChecker CreateChecker(
        HttpMessageHandler? handler = null,
        ClientCertificateRevocationOptions? options = null
    )
    {
        if (handler is not null)
        {
            MockHttpClientFactory
                .Setup(x =>
                    x.CreateClient(DefaultClientCertificateRevocationChecker.HttpClientName)
                )
                .Returns(() => new HttpClient(handler, disposeHandler: false))
                .Verifiable();
        }

        return new DefaultClientCertificateRevocationChecker(
            TimeProvider.System,
            MockHttpClientFactory.Object,
            new DefaultOutboundHttpContentReader(),
            MemoryCache,
            Options.Create(options ?? new ClientCertificateRevocationOptions()),
            CreateLooseMock<ILogger<DefaultClientCertificateRevocationChecker>>().Object
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
        var checker = CreateChecker(handler);

        var result = await checker.CheckAsync(leaf, root, CancellationToken.None);

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
        var checker = CreateChecker(handler);

        var result = await checker.CheckAsync(leaf, root, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WithoutADistributionPoint_ReturnsUnknownWithoutFetching()
    {
        using var root = TestCertificates.CreateRootAuthority("Root");
        using var leaf = TestCertificates.CreateClientCertificate(root, "CN=c");
        var checker = CreateChecker();

        var result = await checker.CheckAsync(leaf, root, CancellationToken.None);

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
        var checker = CreateChecker();

        var result = await checker.CheckAsync(leaf, root, CancellationToken.None);

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
        var checker = CreateChecker(handler);

        var result = await checker.CheckAsync(leaf, root, CancellationToken.None);

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
        var checker = CreateChecker(handler);

        var result = await checker.CheckAsync(leaf, root, CancellationToken.None);

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
        var checker = CreateChecker(handler);

        var result = await checker.CheckAsync(leaf, root, CancellationToken.None);

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
        var checker = CreateChecker(handler);

        var result = await checker.CheckAsync(leaf, root, CancellationToken.None);

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
        var checker = CreateChecker(handler);

        var result = await checker.CheckAsync(leaf, root, CancellationToken.None);

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
        var checker = CreateChecker(handler);

        var first = await checker.CheckAsync(leaf, root, CancellationToken.None);
        var second = await checker.CheckAsync(leaf, root, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Revoked, first);
        Assert.Equal(ClientCertificateRevocationStatus.Revoked, second);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task CheckAsync_WhenAnotherIssuerNamesTheSameUri_DoesNotReuseTheCachedCrl()
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
        var checker = CreateChecker(handler);

        var first = await checker.CheckAsync(leaf, root, CancellationToken.None);
        var second = await checker.CheckAsync(otherLeaf, otherRoot, CancellationToken.None);

        Assert.Equal(ClientCertificateRevocationStatus.Good, first);
        Assert.Equal(ClientCertificateRevocationStatus.Unknown, second);
        Assert.Equal(2, handler.RequestCount);
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
        var checker = CreateChecker(handler);

        var first = await checker.CheckAsync(leaf, root, CancellationToken.None);
        var second = await checker.CheckAsync(leaf, root, CancellationToken.None);

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
        var checker = CreateChecker(
            handler,
            new ClientCertificateRevocationOptions { MaxResponseSizeBytes = 16 }
        );

        var result = await checker.CheckAsync(leaf, root, CancellationToken.None);

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
        var checker = CreateChecker(
            handler,
            new ClientCertificateRevocationOptions { FetchTimeout = TimeSpan.FromMilliseconds(100) }
        );

        var result = await checker.CheckAsync(leaf, root, CancellationToken.None);

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
        var checker = CreateChecker(handler);

        var first = checker.CheckAsync(leaf, root, CancellationToken.None).AsTask();
        var second = checker.CheckAsync(leaf, root, CancellationToken.None).AsTask();
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
        var checker = CreateChecker(handler);
        using var cancellation = new CancellationTokenSource();
        var issuerCopy = X509CertificateLoader.LoadCertificate(root.RawData);

        var first = checker.CheckAsync(leaf, issuerCopy, cancellation.Token).AsTask();
        await cancellation.CancelAsync();
        issuerCopy.Dispose();
        var second = checker.CheckAsync(leaf, root, CancellationToken.None).AsTask();
        gate.SetResult();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
        Assert.Equal(ClientCertificateRevocationStatus.Good, await second);
        Assert.Equal(1, handler.RequestCount);
    }

    #endregion
}
