// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Results;
using NCode.Identity.Registration.AspNetCore;
using NCode.Mediator;
using NCode.Mediator.Middleware;

namespace NCode.Identity.OpenId.Exceptions;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdExceptionHandler"/> abstraction.
/// </summary>
internal class DefaultOpenIdExceptionHandler(ILogger<DefaultOpenIdExceptionHandler> logger)
    : IOpenIdExceptionHandler
{
    private ILogger<DefaultOpenIdExceptionHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<ReadOnlyEndpointDisposition> HandleExceptionAsync(
        OpenIdEnvironment openIdEnvironment,
        HttpContext httpContext,
        IMediator mediator,
        ExceptionDispatchInfo exceptionDispatchInfo,
        CancellationToken cancellationToken
    )
    {
        var exceptionState = new CommandExceptionHandlerState();

        await mediator.SendAsync(
            new OnUnhandledExceptionCommand(
                openIdEnvironment,
                httpContext,
                mediator,
                exceptionDispatchInfo,
                exceptionState
            ),
            cancellationToken
        );

        if (exceptionState.IsHandled)
        {
            return ReadOnlyEndpointDisposition.Handled();
        }

        var exception = exceptionDispatchInfo.SourceException;

        // A protocol error that names no status code is a client error: an OAuth 2.0 error response is 400 Bad Request
        // unless the endpoint's specification says otherwise (RFC 6749 §5.2, RFC 9126 §2.3).
        if (exception is OpenIdException { Error: { StatusCode: null } protocolError })
            protocolError.StatusCode = StatusCodes.Status400BadRequest;

        LogException(httpContext, exception);

        var httpResult = CreateHttpResult(openIdEnvironment, exception);
        return ReadOnlyEndpointDisposition.Handled(httpResult);
    }

    private void LogException(HttpContext httpContext, Exception exception)
    {
        var requestToString = HttpContextDebugFormatter.RequestToString(httpContext.Request);

        // The owner of the outage (such as the key ring for an unavailable root key) already logs and audits it, so
        // each refused request is a Warning rather than an Error.
        if (
            exception is OpenIdException
            {
                Error.Code: OpenIdProtocol.ErrorCodes.TemporarilyUnavailable
            } temporaryError
        )
        {
            Logger.TemporarilyUnavailable(exception, requestToString, temporaryError.Error.Code);
            return;
        }

        // A client-facing 4xx OpenIdException (e.g. an invalid redirect_uri or malformed request) is an expected
        // validation outcome, not a server fault; log it at Warning so genuine 5xx faults stay the only Error entries.
        if (exception is OpenIdException { Error: { StatusCode: >= 400 and < 500 } error })
        {
            Logger.ClientError(exception, requestToString, error.Code);
            return;
        }

        Logger.UnhandledException(exception, requestToString);
    }

    private static IResult CreateHttpResult(
        OpenIdEnvironment openIdEnvironment,
        Exception exception
    ) =>
        exception is OpenIdException openIdException
            ? openIdException.Error.AsHttpResult()
            : openIdEnvironment
                .CreateError(OpenIdProtocol.ErrorCodes.ServerError)
                .WithDescription("An unexpected error occurred while processing the request.")
                .WithStatusCode(StatusCodes.Status500InternalServerError)
                .WithException(exception)
                .AsHttpResult();
}
