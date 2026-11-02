// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NCode.Identity.Bff.Options;
using NCode.Identity.Bff.RemoteApis;
using NCode.Identity.OpenId;
using Xunit;
using Yarp.ReverseProxy.Forwarder;

namespace NCode.Identity.Bff.Tests.RemoteApis;

public class BffRemoteApiHandlerTests : BaseTests
{
    private const string Destination = "https://api.example.com/v1";

    private Mock<IHttpForwarder> MockForwarder { get; }

    private Mock<IBffTokenAccessor> MockTokens { get; }

    private FakeLogger<BffRemoteApiHandler> Logger { get; } = new();

    private BffRemoteApi Api { get; }

    public BffRemoteApiHandlerTests()
    {
        MockForwarder = CreateStrictMock<IHttpForwarder>();
        MockTokens = CreateStrictMock<IBffTokenAccessor>();
        Api = new BffRemoteApi(
            "/api/orders",
            Destination,
            new BffTokenRequest(BffTokenRequirementNames.User),
            ["GET", "POST"],
            TimeSpan.FromSeconds(30),
            new HttpMessageInvoker(new HttpClientHandler())
        );
    }

    private BffRemoteApiHandler CreateHandler() =>
        new(
            MockForwarder.Object,
            MockTokens.Object,
            Microsoft.Extensions.Options.Options.Create(new BffOptions()),
            Logger
        );

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.RouteValues[BffRemoteApiPaths.RemainderParameter] = path[
            "/api/orders/".Length..
        ];
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        return await JsonSerializer.DeserializeAsync<JsonElement>(context.Response.Body);
    }

    private void SetupToken(HttpContext context, BffTokenResult result) =>
        MockTokens
            .Setup(x => x.GetAccessTokenAsync(context, Api.TokenRequest, context.RequestAborted))
            .Returns(new ValueTask<BffTokenResult>(result))
            .Verifiable();

    private static BffTokenResult BearerToken { get; } =
        BffTokenResult.Success(
            new BffAccessToken("at", OpenIdProtocol.TokenTypes.Bearer, DateTimeOffset.MaxValue)
        );

    private void SetupForwarder(HttpContext context, ForwarderError error) =>
        MockForwarder
            .Setup(x =>
                x.SendAsync(
                    context,
                    Destination,
                    Api.Invoker,
                    Api.RequestConfig,
                    It.IsAny<BffRemoteApiTransformer>()
                )
            )
            .Returns(new ValueTask<ForwarderError>(error))
            .Verifiable();

    #region HandleAsync Tests

    [Fact]
    public async Task HandleAsync_MethodNotAllowed_Returns405WithAllow()
    {
        var context = CreateContext(HttpMethods.Delete, "/api/orders/1");

        await CreateHandler().HandleAsync(context, Api);

        Assert.Equal(StatusCodes.Status405MethodNotAllowed, context.Response.StatusCode);
        Assert.Equal("GET, POST", context.Response.Headers.Allow.ToString());
        var problem = await ReadProblemAsync(context);
        Assert.Equal(BffProblemTypes.MethodNotAllowed, problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task HandleAsync_UnsafePath_Returns400()
    {
        var context = CreateContext(HttpMethods.Get, "/api/orders/../admin");

        await CreateHandler().HandleAsync(context, Api);

        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        var problem = await ReadProblemAsync(context);
        Assert.Equal(BffProblemTypes.InvalidRequest, problem.GetProperty("type").GetString());
    }

    [Fact]
    public async Task HandleAsync_TokenUnavailable_Throws()
    {
        var context = CreateContext(HttpMethods.Get, "/api/orders/1");
        SetupToken(
            context,
            BffTokenResult.Failure(
                StatusCodes.Status401Unauthorized,
                BffTokenFailureReasons.SessionRequired
            )
        );

        var exception = await Assert.ThrowsAsync<BffTokenUnavailableException>(() =>
            CreateHandler().HandleAsync(context, Api)
        );

        Assert.Equal(StatusCodes.Status401Unauthorized, exception.StatusCode);
        Assert.Equal(BffTokenFailureReasons.SessionRequired, exception.Reason);
    }

    [Fact]
    public async Task HandleAsync_Forwarded_LogsNothing()
    {
        var context = CreateContext(HttpMethods.Get, "/api/orders/1");
        SetupToken(context, BearerToken);
        SetupForwarder(context, ForwarderError.None);

        await CreateHandler().HandleAsync(context, Api);

        Assert.Equal(0, Logger.Collector.Count);
    }

    [Fact]
    public async Task HandleAsync_ForwarderError_LogsAWarning()
    {
        var context = CreateContext(HttpMethods.Get, "/api/orders/1");
        var failure = new HttpRequestException("refused");
        context.Features.Set<IForwarderErrorFeature>(
            new TestForwarderErrorFeature(ForwarderError.Request, failure)
        );
        SetupToken(context, BearerToken);
        SetupForwarder(context, ForwarderError.Request);

        await CreateHandler().HandleAsync(context, Api);

        var record = Assert.Single(Logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Same(failure, record.Exception);
        Assert.Contains("/api/orders", record.Message, StringComparison.Ordinal);
        Assert.Contains(nameof(ForwarderError.Request), record.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task HandleAsync_ForwarderErrorAfterAbort_LogsNothing()
    {
        var context = CreateContext(HttpMethods.Get, "/api/orders/1");
        using var aborted = new CancellationTokenSource();
        await aborted.CancelAsync();
        context.RequestAborted = aborted.Token;
        SetupToken(context, BearerToken);
        SetupForwarder(context, ForwarderError.RequestCanceled);

        await CreateHandler().HandleAsync(context, Api);

        Assert.Equal(0, Logger.Collector.Count);
    }

    #endregion

    private sealed class TestForwarderErrorFeature(ForwarderError error, Exception? exception)
        : IForwarderErrorFeature
    {
        public ForwarderError Error { get; } = error;

        public Exception? Exception { get; } = exception;
    }
}
