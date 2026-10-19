#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Results;
using NCode.Mediator;
using NCode.Mediator.Middleware;
using NCode.Registration.AspNetCore;

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
                .CreateError(OpenIdConstants.ErrorCodes.ServerError)
                .WithDescription("An unexpected error occurred while processing the request.")
                .WithStatusCode(StatusCodes.Status500InternalServerError)
                .WithException(exception)
                .AsHttpResult();
}
