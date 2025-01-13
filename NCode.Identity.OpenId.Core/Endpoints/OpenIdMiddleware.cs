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

internal class OpenIdMiddleware(
    RequestDelegate next,
    IOpenIdErrorFactory errorFactory
)
{
    private RequestDelegate Next { get; } = next;
    private IOpenIdErrorFactory ErrorFactory { get; } = errorFactory;

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await Next(httpContext);
        }
        catch (Exception exception)
        {
            var result = exception switch
            {
                OpenIdException openIdException => openIdException.Error.AsResult(),
                HttpResultException httpResultException => httpResultException.HttpResult,
                _ => ErrorFactory
                    .Create(OpenIdConstants.ErrorCodes.ServerError)
                    .WithException(exception)
                    .AsResult()
            };

            // TODO: use mediator to publish error event

            await result.ExecuteAsync(httpContext);
        }
    }
}
