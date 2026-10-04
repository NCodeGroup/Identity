#region Copyright Preamble

// Copyright @ 2026 NCode Group
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
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Contexts;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// The outermost endpoint filter for the management <c>/api</c> surface. It materializes the OpenID request environment
/// (the server and the request's resolved tenant) and publishes the <see cref="OpenIdContext"/> for endpoints and the
/// authorization handler to read. It does <b>not</b> open a tenant scope: control-plane endpoints act on unscoped
/// server/tenant rows, and tenant-bound endpoints open their scope from the route's <c>tenantId</c> via the
/// <see cref="TenantScopeEndpointFilter"/> on the tenant-plane group.
/// </summary>
internal sealed class ManagementEnvironmentEndpointFilter(
    IOpenIdContextFactory openIdContextFactory
) : IEndpointFilter
{
    private IOpenIdContextFactory OpenIdContextFactory { get; } = openIdContextFactory;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var httpContext = context.HttpContext;
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();

        try
        {
            // The factory publishes the context on the request (an IOpenIdContextFeature) for endpoints to retrieve.
            await OpenIdContextFactory.CreateAsync(
                httpContext,
                mediator,
                httpContext.RequestAborted
            );
        }
        catch (HttpResultException exception)
        {
            return exception.HttpResult;
        }

        return await next(context);
    }
}
