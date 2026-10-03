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
/// A reusable endpoint filter that materializes the shared OpenID request environment for management endpoints: it
/// builds the <see cref="OpenIdContext"/> (the server and the resolved, settings/secrets-bearing tenant) and publishes
/// it on the request so every management endpoint can bind it (ADR-0036). Opening the ambient tenant data-scope is a
/// separate concern handled by <see cref="AmbientTenantScopeEndpointFilter"/>, so cross-tenant endpoints (tenants,
/// servers) run the environment without being scoped to a single tenant's rows.
/// </summary>
internal sealed class OpenIdEnvironmentEndpointFilter(IOpenIdContextFactory openIdContextFactory)
    : IEndpointFilter
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
            // The factory publishes the context on the request (an IOpenIdContextFeature) so endpoints can bind it.
            await OpenIdContextFactory.CreateAsync(
                httpContext,
                mediator,
                httpContext.RequestAborted
            );
        }
        catch (HttpResultException exception)
        {
            // An unresolvable tenant (unknown host/path) is reported as its mapped result (typically 404).
            return exception.HttpResult;
        }

        return await next(context);
    }
}
