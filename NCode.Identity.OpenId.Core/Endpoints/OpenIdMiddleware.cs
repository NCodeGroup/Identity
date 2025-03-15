#region Copyright Preamble

//
//    Copyright @ 2023 NCode Group
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
using Microsoft.AspNetCore.Mvc;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Mediator;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints;

internal class OpenIdMiddleware(
    RequestDelegate next,
    IOpenIdExceptionHandler openIdExceptionHandler,
    IOpenIdEnvironmentProvider openIdEnvironmentProvider
)
{
    private RequestDelegate Next { get; } = next;
    private IOpenIdExceptionHandler OpenIdExceptionHandler { get; } = openIdExceptionHandler;
    private IOpenIdEnvironmentProvider OpenIdEnvironmentProvider { get; } = openIdEnvironmentProvider;

    public async Task InvokeAsync(HttpContext httpContext, [FromServices] IMediator mediator)
    {
        IResult httpResult;
        try
        {
            await Next(httpContext);
            return;
        }
        catch (HttpResultException exception)
        {
            httpResult = exception.HttpResult;
        }
        catch (OpenIdException exception)
        {
            httpResult = exception.Error.AsHttpResult();
        }
        catch (Exception exception)
        {
            // if this fails, there is nothing we can do
            var openIdEnvironment = OpenIdEnvironmentProvider.Get();

            var exceptionDispatchInfo = ExceptionDispatchInfo.Capture(exception);

            var disposition = await HandleExceptionAsync(
                openIdEnvironment,
                httpContext,
                mediator,
                exceptionDispatchInfo,
                httpContext.RequestAborted
            );

            if (!disposition.WasHandled)
            {
                exceptionDispatchInfo.Throw();
            }

            if (!disposition.HasHttpResult)
            {
                return;
            }

            httpResult = disposition.HttpResult;
        }

        await httpResult.ExecuteAsync(httpContext);
    }

    private async ValueTask<EndpointDisposition> HandleExceptionAsync(
        OpenIdEnvironment openIdEnvironment,
        HttpContext httpContext,
        IMediator mediator,
        ExceptionDispatchInfo exceptionDispatchInfo,
        CancellationToken cancellationToken
    )
    {
        var openIdExceptionHandler = OpenIdExceptionHandler;

        var endpoint = httpContext.GetEndpoint();
        var metadata = endpoint?.Metadata.GetMetadata<IOpenIdEndpointExceptionHandlerMetadata>();
        if (metadata is not null)
        {
            openIdExceptionHandler = await metadata.GetExceptionHandlerAsync(
                httpContext,
                openIdEnvironment,
                cancellationToken
            );
        }

        var disposition = await openIdExceptionHandler.HandleExceptionAsync(
            openIdEnvironment,
            httpContext,
            mediator,
            exceptionDispatchInfo,
            cancellationToken
        );
        return disposition;
    }
}
