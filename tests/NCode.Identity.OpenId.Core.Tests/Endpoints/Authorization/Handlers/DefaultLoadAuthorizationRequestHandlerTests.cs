// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Primitives;
using Moq;
using NCode.Collections.Providers;
using NCode.Identity.Jose;
using NCode.Identity.Jwt.Validation;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Commands;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Handlers;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Messages;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Messages;
using NCode.Identity.OpenId.Authentication.OutboundHttp;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Identity.OpenId.Messages.Parameters;
using NCode.Identity.OpenId.Messages.Parsers;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints.Authorization.Handlers;

public class DefaultLoadAuthorizationRequestHandlerTests : BaseTests
{
    #region Scaffold

    private const string RequestUri = "https://client.example.com/request.jwt";

    private Mock<IHttpClientFactory> MockHttpClientFactory { get; }
    private Mock<OpenIdEnvironment> MockOpenIdEnvironment { get; }
    private FakeLogger<DefaultLoadAuthorizationRequestHandler> Logger { get; } = new();
    private string? CapturedErrorCode { get; set; }

    public DefaultLoadAuthorizationRequestHandlerTests()
    {
        MockHttpClientFactory = CreateStrictMock<IHttpClientFactory>();
        MockOpenIdEnvironment = CreateStrictMock<OpenIdEnvironment>();
    }

    // None of the JOSE serializer, the JWT validator or the client key resolver is reached before the request object
    // is fetched, so a strict mock without setups proves it.
    private DefaultLoadAuthorizationRequestHandler CreateHandler() =>
        new(
            Logger,
            CreateStrictMock<IJoseSerializer>().Object,
            CreateStrictMock<IJwtValidator>().Object,
            MockHttpClientFactory.Object,
            new DefaultOutboundHttpContentReader(),
            CreateStrictMock<IClientKeyResolver>().Object
        );

    // The common parameters describe how each value a request carries is parsed.
    private void SetupKnownParameters()
    {
        var dataSource = new DefaultCommonParameterDataSource(NullChangeToken.Singleton);
        var knownParameters = dataSource.Collection.ToDictionary(knownParameter =>
            knownParameter.Name
        );
        MockOpenIdEnvironment
            .Setup(x => x.GetParameterDescriptor(It.IsAny<string>()))
            .Returns(
                (string parameterName) =>
                    knownParameters.TryGetValue(parameterName, out var knownParameter)
                        ? new ParameterDescriptor(knownParameter)
                        : new ParameterDescriptor(parameterName, ParameterLoader.Default)
            )
            .Verifiable();
    }

    // What a path does with the error the factory creates: a path that never fails with an error creates none.
    private enum ErrorShape
    {
        None,
        WithoutException,
        WithException,
    }

    // The error factory answers every code with a strict error and remembers the last code it was asked for. The
    // error takes a description and, when the path attaches one, an exception; the exception is thrown with the code.
    private void SetupErrorFactory(ErrorShape errorShape)
    {
        var mockErrorFactory = CreateStrictMock<IOpenIdErrorFactory>();
        if (errorShape != ErrorShape.None)
        {
            var mockError = CreateStrictMock<IOpenIdError>();
            Exception? errorException = null;
            mockError.SetupSet(x => x.Description = It.IsAny<string?>()).Verifiable();
            if (errorShape == ErrorShape.WithException)
            {
                mockError
                    .SetupSet(x => x.Exception = It.IsAny<Exception?>())
                    .Callback<Exception?>(exception => errorException = exception)
                    .Verifiable();
            }
            mockError.SetupGet(x => x.Exception).Returns(() => errorException).Verifiable();
            mockError.SetupGet(x => x.Code).Returns(() => CapturedErrorCode ?? "").Verifiable();

            mockErrorFactory
                .Setup(x => x.Create(It.IsAny<string>()))
                .Callback<string>(code => CapturedErrorCode = code)
                .Returns(mockError.Object)
                .Verifiable();
        }

        MockOpenIdEnvironment
            .Setup(x => x.ErrorFactory)
            .Returns(mockErrorFactory.Object)
            .Verifiable();
    }

    private OpenIdContext CreateContext(ErrorShape errorShape = ErrorShape.WithException)
    {
        SetupKnownParameters();
        SetupErrorFactory(errorShape);

        var mockContext = CreateStrictMock<OpenIdContext>();
        mockContext.Setup(x => x.Environment).Returns(MockOpenIdEnvironment.Object).Verifiable();
        return mockContext.Object;
    }

    // The settings are strict: every path that sends reads the supported flag, the allowed hosts, the fetch timeout and
    // the size limit, and only a path that gets a 200 response also reads the content-type settings.
    private OpenIdClient CreateClient(
        TimeSpan? fetchTimeout = null,
        bool readsResponseSettings = false
    )
    {
        var mockSettings = CreateStrictMock<IReadOnlySettingCollection>();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RequestUriParameterSupported))
            .Returns(true)
            .Verifiable();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RequestUriFetchTimeout))
            .Returns(fetchTimeout ?? TimeSpan.FromSeconds(10))
            .Verifiable();
        mockSettings
            .Setup(x => x.GetValue(OpenIdSettingKeys.RequestUriMaxSizeBytes))
            .Returns(100 * 1024)
            .Verifiable();
        if (readsResponseSettings)
        {
            mockSettings
                .Setup(x => x.GetValue(OpenIdSettingKeys.RequestUriExpectedContentType))
                .Returns("application/oauth-authz-req+jwt")
                .Verifiable();
            mockSettings
                .Setup(x => x.GetValue(OpenIdSettingKeys.RequestUriRequireStrictContentType))
                .Returns(true)
                .Verifiable();
        }

        IReadOnlyCollection<string>? allowedHosts = null;
        mockSettings
            .Setup(x => x.TryGetValue(OpenIdSettingKeys.RequestUriAllowedHosts, out allowedHosts))
            .Returns(false)
            .Verifiable();

        var mockClient = CreateStrictMock<OpenIdClient>();
        mockClient.Setup(x => x.Settings).Returns(mockSettings.Object).Verifiable();
        return mockClient.Object;
    }

    private static RequestValuesUsingCollection CreateRequestValues(string requestUri)
    {
        var values = new Dictionary<string, StringValues>
        {
            ["client_id"] = "client-1",
            ["response_type"] = "code",
            ["scope"] = "openid",
            ["request_uri"] = requestUri,
        };
        return new RequestValuesUsingCollection("query", values);
    }

    private void SetupHttpClient(HttpClient httpClient) =>
        MockHttpClientFactory
            .Setup(x => x.CreateClient(GuardedHttpClient.Name))
            .Returns(httpClient)
            .Verifiable();

    private ValueTask<IAuthorizationRequest> HandleAsync(
        OpenIdContext context,
        OpenIdClient client,
        string requestUri,
        CancellationToken cancellationToken
    ) =>
        CreateHandler()
            .HandleAsync(
                new LoadAuthorizationRequestCommand(
                    context,
                    client,
                    CreateRequestValues(requestUri)
                ),
                cancellationToken
            );

    private void AssertFetchFailedReason(string reason)
    {
        var record = Assert.Single(
            Logger.Collector.GetSnapshot(),
            r => r.Id.Id == EventIds.FailedToFetchRequestUri
        );
        Assert.Equal(reason, record.GetStructuredStateValue("Reason"));
    }

    private void AssertNoFetchFailedRecord() =>
        Assert.DoesNotContain(
            Logger.Collector.GetSnapshot(),
            r => r.Id.Id == EventIds.FailedToFetchRequestUri
        );

    private sealed class StubHttpMessageHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class ThrowingHttpMessageHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        ) => Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class BodyHttpMessageHandler(byte[] body, string contentType)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var content = new ByteArrayContent(body);
            content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = content }
            );
        }
    }

    private sealed class FailingReadStream : Stream
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

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default
        ) => ValueTask.FromException<int>(new IOException("x"));

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new IOException("x");

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }

    private sealed class FailingBodyHttpMessageHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            var content = new StreamContent(new FailingReadStream());
            content.Headers.ContentType = new MediaTypeHeaderValue(
                "application/oauth-authz-req+jwt"
            );
            return Task.FromResult(
                new HttpResponseMessage(HttpStatusCode.OK) { Content = content }
            );
        }
    }

    private sealed class DelayingHttpMessageHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken
        )
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    #endregion

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_WhenTheRequestUriAnswersANonOkStatus_ThrowsInvalidRequestUriWithoutAFetchLog()
    {
        var context = CreateContext(ErrorShape.WithoutException);
        var client = CreateClient();
        SetupHttpClient(new HttpClient(new StubHttpMessageHandler(HttpStatusCode.NotFound)));

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await HandleAsync(context, client, RequestUri, CancellationToken.None)
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestUri, CapturedErrorCode);
        AssertNoFetchFailedRecord();
    }

    [Fact]
    public async Task HandleAsync_WhenTheResponseIsNotUtf8_ThrowsInvalidRequestUriWithoutAFetchLog()
    {
        var context = CreateContext();
        var client = CreateClient(readsResponseSettings: true);
        SetupHttpClient(
            new HttpClient(
                new BodyHttpMessageHandler([0xC3, 0x28], "application/oauth-authz-req+jwt")
            )
        );

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await HandleAsync(context, client, RequestUri, CancellationToken.None)
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestUri, CapturedErrorCode);
        AssertNoFetchFailedRecord();
    }

    [Fact]
    public async Task HandleAsync_WhenTheHostResolvesToARefusedAddress_LogsBlockedAddress()
    {
        var context = CreateContext();
        var client = CreateClient();
        using var handler = new SocketsHttpHandler
        {
            ConnectCallback = new DefaultOutboundHttpForgeryGuard([]).ConnectAsync,
        };
        SetupHttpClient(new HttpClient(handler, disposeHandler: false));

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await HandleAsync(
                context,
                client,
                "https://10.0.0.5/request.jwt",
                CancellationToken.None
            )
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestUri, CapturedErrorCode);
        AssertFetchFailedReason("blocked_address");
    }

    [Fact]
    public async Task HandleAsync_WhenTheConnectionFails_LogsConnection()
    {
        var context = CreateContext();
        var client = CreateClient();
        SetupHttpClient(
            new HttpClient(new ThrowingHttpMessageHandler(new HttpRequestException("x")))
        );

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await HandleAsync(context, client, RequestUri, CancellationToken.None)
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestUri, CapturedErrorCode);
        AssertFetchFailedReason("connection");
    }

    [Fact]
    public async Task HandleAsync_WhenTheResponseBodyReadFails_LogsConnection()
    {
        var context = CreateContext();
        var client = CreateClient(readsResponseSettings: true);
        SetupHttpClient(new HttpClient(new FailingBodyHttpMessageHandler()));

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await HandleAsync(context, client, RequestUri, CancellationToken.None)
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestUri, CapturedErrorCode);
        AssertFetchFailedReason("connection");
    }

    [Fact]
    public async Task HandleAsync_WhenTheFetchTimesOut_LogsTimeout()
    {
        var context = CreateContext();
        var client = CreateClient(fetchTimeout: TimeSpan.FromMilliseconds(50));
        SetupHttpClient(new HttpClient(new DelayingHttpMessageHandler()));

        await Assert.ThrowsAsync<OpenIdException>(async () =>
            await HandleAsync(context, client, RequestUri, CancellationToken.None)
        );

        Assert.Equal(OpenIdProtocol.ErrorCodes.InvalidRequestUri, CapturedErrorCode);
        AssertFetchFailedReason("timeout");
    }

    [Fact]
    public async Task HandleAsync_WhenTheCallerCancels_ThrowsOperationCanceledWithoutAFetchLog()
    {
        var context = CreateContext(ErrorShape.None);
        var client = CreateClient(fetchTimeout: TimeSpan.FromSeconds(10));
        SetupHttpClient(new HttpClient(new DelayingHttpMessageHandler()));
        using var callerCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await HandleAsync(context, client, RequestUri, callerCts.Token)
        );

        AssertNoFetchFailedRecord();
    }

    #endregion
}
