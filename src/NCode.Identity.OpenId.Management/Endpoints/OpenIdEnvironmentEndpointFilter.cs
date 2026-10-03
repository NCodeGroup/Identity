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
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Management.Endpoints;

/// <summary>
/// A reusable endpoint filter that materializes the shared OpenID request environment for tenant-bound management
/// endpoints: it builds the <see cref="OpenIdContext"/> (the server and the resolved, settings/secrets-bearing tenant)
/// and opens the ambient tenant scope that tenant-scoped data access reads (ADR-0018/ADR-0036). Management requests thus
/// run the same environment the OpenID protocol endpoints do, rather than a separate tenant-only path.
/// </summary>
internal sealed class OpenIdEnvironmentEndpointFilter(
    IOpenIdContextFactory openIdContextFactory,
    IAmbientTenantAccessor ambientTenantAccessor
) : IEndpointFilter
{
    private IOpenIdContextFactory OpenIdContextFactory { get; } = openIdContextFactory;

    private IAmbientTenantAccessor AmbientTenantAccessor { get; } = ambientTenantAccessor;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var httpContext = context.HttpContext;
        var mediator = httpContext.RequestServices.GetRequiredService<IMediator>();

        OpenIdContext openIdContext;
        try
        {
            openIdContext = await OpenIdContextFactory.CreateAsync(
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

        using var scope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);
        return await next(context);
    }
}
