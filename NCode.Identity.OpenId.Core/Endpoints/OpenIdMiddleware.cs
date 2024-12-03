﻿#region Copyright Preamble

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
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;

namespace NCode.Identity.OpenId.Endpoints;

internal class OpenIdMiddleware(
    RequestDelegate next,
    OpenIdEnvironment openIdEnvironment)
{
    private RequestDelegate Next { get; } = next;
    private OpenIdEnvironment OpenIdEnvironment { get; } = openIdEnvironment;

    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await Next(httpContext);
        }
        catch (OpenIdException exception)
        {
            var openIdError = exception.Error;

            var result = TypedResults.Json(
                openIdError,
                OpenIdEnvironment.JsonSerializerOptions,
                statusCode: openIdError.StatusCode);

            await result.ExecuteAsync(httpContext);
        }
        catch (HttpResultException exception)
        {
            await exception.HttpResult.ExecuteAsync(httpContext);
        }
    }
}
