#region Copyright Preamble

//
//    Copyright @ 2021 NCode Group
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

using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using NIdentity.OpenId.Handlers;
using NIdentity.OpenId.Requests;

namespace NIdentity.OpenId.Playground
{
    internal static class HttpEndpointHandlerRouteBuilderExtensions
    {
        public static IEndpointConventionBuilder MapGet<TRequest>(this IEndpointRouteBuilder endpoints, string pattern, Func<HttpContext, TRequest> requestFactory)
            where TRequest : ProcessHttpEndpoint
        {
            return endpoints.MapGet(pattern, async httpContext =>
            {
                var request = requestFactory(httpContext);
                var handler = httpContext.RequestServices.GetRequiredService<IHttpEndpointHandler<TRequest>>();
                var httpResult = await handler.HandleAsync(request, httpContext.RequestAborted);
                await httpResult.ExecuteAsync(httpContext);
            });
        }

        public static IEndpointConventionBuilder MapPost<TRequest>(this IEndpointRouteBuilder endpoints, string pattern, Func<HttpContext, TRequest> requestFactory)
            where TRequest : ProcessHttpEndpoint
        {
            return endpoints.MapPost(pattern, async httpContext =>
            {
                var request = requestFactory(httpContext);
                var handler = httpContext.RequestServices.GetRequiredService<IHttpEndpointHandler<TRequest>>();
                var httpResult = await handler.HandleAsync(request, httpContext.RequestAborted);
                await httpResult.ExecuteAsync(httpContext);
            });
        }

        public static IEndpointConventionBuilder MapMethods<TRequest>(this IEndpointRouteBuilder endpoints, string pattern, IEnumerable<string> httpMethods, Func<HttpContext, TRequest> requestFactory)
            where TRequest : ProcessHttpEndpoint
        {
            return endpoints.MapMethods(pattern, httpMethods, async httpContext =>
            {
                var request = requestFactory(httpContext);
                var handler = httpContext.RequestServices.GetRequiredService<IHttpEndpointHandler<TRequest>>();
                var httpResult = await handler.HandleAsync(request, httpContext.RequestAborted);
                await httpResult.ExecuteAsync(httpContext);
            });
        }
    }
}
