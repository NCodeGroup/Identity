#region Copyright Preamble

//
//    Copyright @ 2022 NCode Group
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

using Microsoft.AspNetCore.Http;
using NIdentity.OpenId.Logic;
using NIdentity.OpenId.Validation;

namespace NIdentity.OpenId.Middleware;

internal class OpenIdMiddleware : IMiddleware
{
    private IOpenIdExceptionFactory OpenIdExceptionFactory { get; }
    private IExceptionService ExceptionService { get; }

    public OpenIdMiddleware(IOpenIdExceptionFactory openIdExceptionFactory, IExceptionService exceptionService)
    {
        OpenIdExceptionFactory = openIdExceptionFactory;
        ExceptionService = exceptionService;
    }

    public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
    {
        OpenIdException? openIdException = null;
        try
        {
            httpContext.Request.EnableBuffering();

            await next(httpContext);
        }
        catch (OpenIdException exception)
        {
            openIdException = exception;
        }
        catch (Exception exception)
        {
            openIdException = OpenIdExceptionFactory.Create(OpenIdConstants.ErrorCodes.ServerError, exception);
        }

        if (openIdException != null)
        {
            var messageStateFeature = httpContext.Features.Get<IOpenIdMessageStateFeature>();
            var messageState = messageStateFeature?.State;
            if (!string.IsNullOrEmpty(messageState))
                openIdException.WithExtensionData(OpenIdConstants.Parameters.State, messageState);

            var httpResult = ExceptionService.GetHttpResultForException(openIdException);
            await httpResult.ExecuteAsync(httpContext);
        }
    }
}
