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
using NCode.Identity.OpenId.Endpoints;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Errors;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Mediator.Middleware;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Exceptions;

/// <summary>
/// Provides a default implementation of the <see cref="IOpenIdExceptionHandler"/> abstraction.
/// </summary>
public class DefaultOpenIdExceptionHandler(
    ILogger<DefaultOpenIdExceptionHandler> logger
) : IOpenIdExceptionHandler
{
    private ILogger<DefaultOpenIdExceptionHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<EndpointDisposition> HandleExceptionAsync(
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
            return EndpointDisposition.Handled();
        }

        var exception = exceptionDispatchInfo.SourceException;
        LogException(httpContext, exception);

        var httpResult = CreateHttpResult(openIdEnvironment, exception);
        return EndpointDisposition.Handled(httpResult);
    }

    private void LogException(HttpContext httpContext, Exception exception)
    {
        var requestToString = HttpContextDebugFormatter.RequestToString(httpContext.Request);
        Logger.LogError(exception, "An unexpected error occurred while processing the request {DisplayName}", requestToString);
    }

    private static IResult CreateHttpResult(OpenIdEnvironment openIdEnvironment, Exception exception) =>
        openIdEnvironment
            .CreateError(OpenIdConstants.ErrorCodes.ServerError)
            .WithDescription("An unexpected error occurred while processing the request.")
            .WithStatusCode(StatusCodes.Status500InternalServerError)
            .WithException(exception)
            .AsHttpResult();
}
