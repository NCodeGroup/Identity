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

using Microsoft.AspNetCore.Http;
using NCode.Identity.OpenId.Exceptions;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Endpoints;

internal class OpenIdMiddleware(RequestDelegate next, IOpenIdExceptionHandler exceptionHandler)
{
    private RequestDelegate Next { get; } = next;
    private IOpenIdExceptionHandler ExceptionHandler { get; } = exceptionHandler;

    public async Task InvokeAsync(HttpContext httpContext)
    {
        IResult result;
        try
        {
            await Next(httpContext);
            return;
        }
        catch (HttpResultException exception)
        {
            result = exception.HttpResult;
        }
        catch (OpenIdException exception)
        {
            result = exception.Error.AsResult();
        }
        catch (Exception exception)
        {
            var exceptionHandler = ExceptionHandler;
            var cancellationToken = httpContext.RequestAborted;
            var metadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<IOpenIdEndpointExceptionHandlerMetadata>();
            if (metadata is not null)
            {
                exceptionHandler = await metadata.GetExceptionHandlerAsync(httpContext, cancellationToken);
            }

            result = await exceptionHandler.HandleExceptionAsync(httpContext, exception, cancellationToken);
        }

        await result.ExecuteAsync(httpContext);
    }
}
