// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Exceptions;

public class DefaultOpenIdExceptionHandlerTests : BaseTests
{
    private FakeLogger<DefaultOpenIdExceptionHandler> Logger { get; } = new();

    private Mock<IMediator> MockMediator { get; }

    private DefaultOpenIdExceptionHandler Handler { get; }

    public DefaultOpenIdExceptionHandlerTests()
    {
        MockMediator = CreateStrictMock<IMediator>();
        MockMediator
            .Setup(x =>
                x.SendAsync(It.IsAny<OnUnhandledExceptionCommand>(), It.IsAny<CancellationToken>())
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable();

        Handler = new DefaultOpenIdExceptionHandler(Logger);
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("idp.example");
        httpContext.Request.Path = "/connect/token";
        return httpContext;
    }

    #region HandleExceptionAsync Tests

    [Fact]
    public async Task HandleExceptionAsync_WhenErrorIsTemporarilyUnavailable_LogsOneWarning()
    {
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        var error = new DefaultOpenIdError(
            mockEnvironment.Object,
            OpenIdProtocol.ErrorCodes.TemporarilyUnavailable
        );
        var exception = new OpenIdException(error);

        var disposition = await Handler.HandleExceptionAsync(
            mockEnvironment.Object,
            CreateHttpContext(),
            MockMediator.Object,
            ExceptionDispatchInfo.Capture(exception),
            CancellationToken.None
        );

        Assert.True(disposition.WasHandled);
        var record = Assert.Single(Logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Warning, record.Level);
        Assert.Equal(EventIds.TemporarilyUnavailable, record.Id.Id);
        Assert.Same(exception, record.Exception);
        Assert.Contains(OpenIdProtocol.ErrorCodes.TemporarilyUnavailable, record.Message);
        Assert.Contains("https://idp.example/connect/token", record.Message);
    }

    [Fact]
    public async Task HandleExceptionAsync_WhenExceptionIsNotAProtocolError_LogsError()
    {
        var mockEnvironment = CreateStrictMock<OpenIdEnvironment>();
        var error = new DefaultOpenIdError(
            mockEnvironment.Object,
            OpenIdProtocol.ErrorCodes.ServerError
        );
        mockEnvironment
            .Setup(x => x.CreateError(OpenIdProtocol.ErrorCodes.ServerError))
            .Returns(error)
            .Verifiable();
        var exception = new InvalidOperationException("boom");

        var disposition = await Handler.HandleExceptionAsync(
            mockEnvironment.Object,
            CreateHttpContext(),
            MockMediator.Object,
            ExceptionDispatchInfo.Capture(exception),
            CancellationToken.None
        );

        Assert.True(disposition.WasHandled);
        var record = Assert.Single(Logger.Collector.GetSnapshot());
        Assert.Equal(LogLevel.Error, record.Level);
        Assert.Equal(EventIds.UnhandledException, record.Id.Id);
        Assert.Same(exception, record.Exception);
    }

    #endregion
}
