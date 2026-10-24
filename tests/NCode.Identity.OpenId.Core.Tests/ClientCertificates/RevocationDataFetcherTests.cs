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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NCode.Identity.OpenId.Authentication.ClientCertificates;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.ClientCertificates;

public class RevocationDataFetcherTests : BaseTests
{
    #region Scaffold

    private const string Location = "http://crl.example.test/root.crl";
    private const string Subject = "CN=c";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }

    public RevocationDataFetcherTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
    }

    private RevocationDataFetcher CreateFetcher(
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

        return new RevocationDataFetcher(
            MockHttpClientFactory.Object,
            new DefaultOutboundHttpContentReader(),
            Options.Create(options ?? new ClientCertificateRevocationOptions()),
            CreateLooseMock<ILogger<RevocationDataFetcher>>().Object
        );
    }

    private static HttpResponseMessage BodyResponse(byte[] body) =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent(body) };

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

    #region GetAsync Tests

    [Fact]
    public async Task GetAsync_WhenTheResponseSucceeds_ReturnsItsBody()
    {
        byte[] body = [0x01, 0x02, 0x03, 0x04];
        var handler = new StubHttpMessageHandler(_ => BodyResponse(body));
        var fetcher = CreateFetcher(handler);

        var result = await fetcher.GetAsync(Location, Location, Subject, CancellationToken.None);

        Assert.Equal(body, result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetAsync_WhenTheResponseFails_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => NotFoundResponse());
        var fetcher = CreateFetcher(handler);

        var result = await fetcher.GetAsync(Location, Location, Subject, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetAsync_WhenTheBodyExceedsTheMaximumSize_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => BodyResponse(new byte[32]));
        var fetcher = CreateFetcher(
            handler,
            new ClientCertificateRevocationOptions { MaxResponseSizeBytes = 16 }
        );

        var result = await fetcher.GetAsync(Location, Location, Subject, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetAsync_WhenTheFetchOutlastsTheTimeout_ReturnsNull()
    {
        var handler = new AsyncStubHttpMessageHandler(
            async (_, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return NotFoundResponse();
            }
        );
        var fetcher = CreateFetcher(
            handler,
            new ClientCertificateRevocationOptions { FetchTimeout = TimeSpan.FromMilliseconds(100) }
        );

        var result = await fetcher.GetAsync(Location, Location, Subject, CancellationToken.None);

        Assert.Null(result);
        Assert.Equal(1, handler.RequestCount);
    }

    [Fact]
    public async Task GetAsync_WhenTheLocationIsNotAbsolute_ReturnsNullWithoutARequest()
    {
        var fetcher = CreateFetcher();

        var result = await fetcher.GetAsync(
            "crl.example.test/root.crl",
            "crl.example.test/root.crl",
            Subject,
            CancellationToken.None
        );

        Assert.Null(result);
    }

    #endregion

    #region PostAsync Tests

    [Fact]
    public async Task PostAsync_SendsTheContentWithItsType()
    {
        const string contentType = "application/ocsp-request";
        byte[] content = [0x0A, 0x0B, 0x0C];
        HttpMethod? sentMethod = null;
        byte[]? sentBody = null;
        string? sentContentType = null;
        var handler = new AsyncStubHttpMessageHandler(
            async (request, cancellationToken) =>
            {
                sentMethod = request.Method;
                sentBody = await request.Content!.ReadAsByteArrayAsync(cancellationToken);
                sentContentType = request.Content.Headers.ContentType?.MediaType;
                return BodyResponse([0x01]);
            }
        );
        var fetcher = CreateFetcher(handler);

        var result = await fetcher.PostAsync(
            Location,
            Subject,
            content,
            contentType,
            CancellationToken.None
        );

        Assert.Equal(new byte[] { 0x01 }, result);
        Assert.Equal(HttpMethod.Post, sentMethod);
        Assert.Equal(content, sentBody);
        Assert.Equal(contentType, sentContentType);
        Assert.Equal(1, handler.RequestCount);
    }

    #endregion
}
