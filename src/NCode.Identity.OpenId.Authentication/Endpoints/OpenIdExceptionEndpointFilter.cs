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
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Exceptions;

namespace NCode.Identity.OpenId.Authentication.Endpoints;

/// <summary>
/// An endpoint filter, installed once on the OpenID endpoint group, that renders exceptions thrown by
/// OpenID endpoints as standard OpenID error responses. The handler is resolved per-request: an endpoint's
/// own <see cref="IOpenIdEndpointExceptionHandlerMetadata"/> takes precedence, otherwise the globally
/// registered <see cref="IOpenIdExceptionHandler"/> is used.
/// </summary>
internal sealed class OpenIdExceptionEndpointFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var httpContext = context.HttpContext;
        try
        {
            return await next(context);
        }
        catch (Exception exception)
        {
            // The OpenId context (environment + mediator) is published on the request by the context factory.
            // If it is absent the request never entered the OpenId pipeline, so let the framework handle it.
            var feature = httpContext.Features.Get<IOpenIdContextFeature>();
            if (feature is null)
                throw;

            var cancellationToken = httpContext.RequestAborted;
            var openIdContext = feature.OpenIdContext;
            var openIdEnvironment = openIdContext.Environment;

            var handler = await GetExceptionHandlerAsync(
                httpContext,
                openIdEnvironment,
                cancellationToken
            );

            var disposition = await handler.HandleExceptionAsync(
                openIdEnvironment,
                httpContext,
                openIdContext.Mediator,
                ExceptionDispatchInfo.Capture(exception),
                cancellationToken
            );

            return disposition.HttpResult;
        }
    }

    private static async ValueTask<IOpenIdExceptionHandler> GetExceptionHandlerAsync(
        HttpContext httpContext,
        OpenIdEnvironment openIdEnvironment,
        CancellationToken cancellationToken
    )
    {
        var metadata = httpContext
            .GetEndpoint()
            ?.Metadata.GetMetadata<IOpenIdEndpointExceptionHandlerMetadata>();

        if (metadata is not null)
            return await metadata.GetExceptionHandlerAsync(
                httpContext,
                openIdEnvironment,
                cancellationToken
            );

        return httpContext.RequestServices.GetRequiredService<IOpenIdExceptionHandler>();
    }
}
