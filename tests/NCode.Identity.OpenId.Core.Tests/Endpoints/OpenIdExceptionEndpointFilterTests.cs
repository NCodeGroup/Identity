// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using NCode.Identity.OpenId.Authentication.Auditing;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Authentication.Endpoints.Authorization.Results;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Messages;
using NCode.Mediator;
using NCode.Registration.AspNetCore;
using Xunit;

namespace NCode.Identity.OpenId.Core.Tests.Endpoints;

public class OpenIdExceptionEndpointFilterTests : BaseTests
{
    private static HttpContext CreateHttpContext(
        IAuditEventRecorder recorder,
        OpenIdContext openIdContext,
        IOpenIdExceptionHandler? handler = null,
        params IOpenIdExceptionTranslator[] translators
    )
    {
        var services = new ServiceCollection();
        services.AddSingleton(recorder);
        services.AddLogging();
        if (handler is not null)
            services.AddSingleton(handler);
        foreach (var translator in translators)
            services.AddSingleton(translator);

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
        httpContext.Features.Set<IOpenIdContextFeature>(
            Mock.Of<IOpenIdContextFeature>(feature => feature.OpenIdContext == openIdContext)
        );
        return httpContext;
    }

    #region InvokeAsync

    [Fact]
    public async Task InvokeAsync_WhenAuthorizationResultCarriesError_AuditsOnce()
    {
        var mockRecorder = CreateLooseMock<IAuditEventRecorder>();
        var mockContext = CreateLooseMock<OpenIdContext>();
        var mockError = CreateLooseMock<IOpenIdError>();
        mockError.SetupGet(x => x.State).Returns("state-1");

        var httpContext = CreateHttpContext(mockRecorder.Object, mockContext.Object);
        var filterContext = EndpointFilterInvocationContext.Create(httpContext);

        // A redirect-delivered authorization error is carried by AuthorizationResult (ISupportOpenIdError),
        // not an OpenIdResult; the funnel must still audit it from the returned result.
        var authorizationResult = new AuthorizationResult(
            mockContext.Object,
            CreateLooseMock<OpenIdClient>().Object,
            new AuthorizationResponseDelivery(
                "https://rp.example/callback",
                new QueryAuthorizationResponseModeHandler(),
                "state-1"
            ),
            mockError.Object
        );

        var filter = new OpenIdExceptionEndpointFilter();
        var result = await filter.InvokeAsync(
            filterContext,
            _ => ValueTask.FromResult<object?>(authorizationResult)
        );

        Assert.Same(authorizationResult, result);
        mockRecorder.Verify(
            x =>
                x.RecordOpenIdErrorAsync(
                    mockContext.Object,
                    mockError.Object,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task InvokeAsync_WhenResultHasNoError_DoesNotAudit()
    {
        var mockRecorder = CreateLooseMock<IAuditEventRecorder>();
        var mockContext = CreateLooseMock<OpenIdContext>();

        var httpContext = CreateHttpContext(mockRecorder.Object, mockContext.Object);
        var filterContext = EndpointFilterInvocationContext.Create(httpContext);

        var okResult = Mock.Of<IResult>();

        var filter = new OpenIdExceptionEndpointFilter();
        var result = await filter.InvokeAsync(
            filterContext,
            _ => ValueTask.FromResult<object?>(okResult)
        );

        Assert.Same(okResult, result);
        mockRecorder.Verify(
            x =>
                x.RecordOpenIdErrorAsync(
                    It.IsAny<OpenIdContext>(),
                    It.IsAny<IOpenIdError>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Never
        );
    }

    [Fact]
    public async Task InvokeAsync_WhenTranslatorAnswersForException_HandlerReceivesOpenIdExceptionAndErrorIsAuditedOnce()
    {
        var mockEnvironment = CreateLooseMock<OpenIdEnvironment>();
        var mockMediator = CreateStrictMock<IMediator>();
        var mockContext = CreateLooseMock<OpenIdContext>();
        mockContext.SetupGet(x => x.Environment).Returns(mockEnvironment.Object).Verifiable();
        mockContext.SetupGet(x => x.Mediator).Returns(mockMediator.Object).Verifiable();

        var mockError = CreateLooseMock<IOpenIdError>();
        mockError.SetupGet(x => x.Code).Returns("temporarily_unavailable");

        var thrown = new InvalidOperationException("outage");

        var mockTranslator = CreateStrictMock<IOpenIdExceptionTranslator>();
        var translatedError = mockError.Object;
        mockTranslator
            .Setup(x => x.TryTranslate(mockEnvironment.Object, thrown, out translatedError))
            .Returns(true)
            .Verifiable();

        var mockRecorder = CreateStrictMock<IAuditEventRecorder>();
        mockRecorder
            .Setup(x =>
                x.RecordOpenIdErrorAsync(
                    mockContext.Object,
                    mockError.Object,
                    It.IsAny<CancellationToken>()
                )
            )
            .Returns(ValueTask.CompletedTask)
            .Verifiable(Times.Once);

        ExceptionDispatchInfo? captured = null;
        var disposition = ReadOnlyEndpointDisposition.Handled(TypedResults.Ok());
        var mockHandler = CreateStrictMock<IOpenIdExceptionHandler>();
        mockHandler
            .Setup(x =>
                x.HandleExceptionAsync(
                    mockEnvironment.Object,
                    It.IsAny<HttpContext>(),
                    mockMediator.Object,
                    It.IsAny<ExceptionDispatchInfo>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .Callback<
                OpenIdEnvironment,
                HttpContext,
                IMediator,
                ExceptionDispatchInfo,
                CancellationToken
            >((_, _, _, info, _) => captured = info)
            .Returns(new ValueTask<ReadOnlyEndpointDisposition>(disposition))
            .Verifiable();

        var httpContext = CreateHttpContext(
            mockRecorder.Object,
            mockContext.Object,
            mockHandler.Object,
            mockTranslator.Object
        );
        var filterContext = EndpointFilterInvocationContext.Create(httpContext);

        var filter = new OpenIdExceptionEndpointFilter();
        var result = await filter.InvokeAsync(filterContext, _ => throw thrown);

        Assert.Same(disposition.HttpResult, result);
        Assert.NotNull(captured);
        var openIdException = Assert.IsType<OpenIdException>(captured.SourceException);
        Assert.Same(mockError.Object, openIdException.Error);
        Assert.Same(thrown, openIdException.InnerException);
    }

    #endregion
}
