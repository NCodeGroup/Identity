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
using NIdentity.OpenId.Exceptions;
using NIdentity.OpenId.Servers;

namespace NIdentity.OpenId.Endpoints;

internal class OpenIdMiddleware(OpenIdServer openIdServer)
{
    private OpenIdServer OpenIdServer { get; } = openIdServer;

    public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
    {
        try
        {
            await next(httpContext);
        }
        catch (OpenIdException exception)
        {
            var error = exception.Error;
            var result = TypedResults.Json(
                error,
                OpenIdServer.JsonSerializerOptions,
                statusCode: error.StatusCode);

            await result.ExecuteAsync(httpContext);
        }
        catch (HttpResultException exception)
        {
            await exception.HttpResult.ExecuteAsync(httpContext);
        }
    }
}
