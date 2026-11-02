// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using Moq;
using NCode.Identity.Dispatch;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Authentication.Sessions;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Sessions;

public class BackChannelLogoutDeliveryJobHandlerTests : BaseTests
{
    private const string ClientId = "client-1";
    private const string LogoutUri = "https://rp.example/backchannel-logout";
    private const string LogoutToken = "header.payload.signature";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private RecordingHttpMessageHandler Handler { get; }
    private BackChannelLogoutDeliveryJobHandler Subject { get; }

    public BackChannelLogoutDeliveryJobHandlerTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        Handler = new RecordingHttpMessageHandler();
        Subject = new BackChannelLogoutDeliveryJobHandler(MockHttpClientFactory.Object);
    }

    private sealed class RecordingHttpMessageHandler(HttpStatusCode statusCode = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            RequestUri = request.RequestUri;
            RequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(statusCode);
        }
    }

    private sealed class UnreadableStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new InvalidOperationException("The response body must not be read.");

        public override int Read(Span<byte> buffer) =>
            throw new InvalidOperationException("The response body must not be read.");

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        ) => throw new InvalidOperationException("The response body must not be read.");

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private sealed class BodyHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) =>
            Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StreamContent(new UnreadableStream()),
                }
            );
    }

    private static DispatchContext Context() =>
        new()
        {
            DispatchId = Guid.NewGuid(),
            Attempt = 1,
            MaxAttempts = 5,
        };

    private static BackChannelLogoutDeliveryJob CreateJob() =>
        new()
        {
            ClientId = ClientId,
            LogoutUri = LogoutUri,
            LogoutToken = LogoutToken,
        };

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenConfigured_PostsFormEncodedLogoutToken()
    {
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(Handler))
            .Verifiable();

        await Subject.HandleAsync(CreateJob(), Context(), CancellationToken.None);

        Assert.Equal(new Uri(LogoutUri), Handler.RequestUri);
        Assert.NotNull(Handler.RequestBody);
        Assert.Contains(OpenIdProtocol.Parameters.LogoutToken, Handler.RequestBody);
        Assert.Contains(LogoutToken, Handler.RequestBody);
    }

    [Fact]
    public async Task HandleAsync_WhenTheResponseHasABody_DoesNotReadIt()
    {
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(new BodyHttpMessageHandler()))
            .Verifiable();

        await Subject.HandleAsync(CreateJob(), Context(), CancellationToken.None);
    }

    [Fact]
    public async Task HandleAsync_WhenEndpointReturnsError_Throws()
    {
        var errorHandler = new RecordingHttpMessageHandler(HttpStatusCode.ServiceUnavailable);
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(new HttpClient(errorHandler))
            .Verifiable();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Subject.HandleAsync(CreateJob(), Context(), CancellationToken.None).AsTask()
        );
    }

    #endregion
}
