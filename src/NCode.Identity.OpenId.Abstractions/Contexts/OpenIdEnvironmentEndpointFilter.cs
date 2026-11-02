// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using NCode.Identity.Exceptions;
using NCode.Identity.OpenId.Persistence.Tenants;
using NCode.Mediator;

namespace NCode.Identity.OpenId.Contexts;

/// <summary>
/// The shared endpoint filter that materializes the OpenID request environment for every OpenID endpoint: it builds the
/// <see cref="OpenIdContext"/> (the server and the resolved, settings/secrets-bearing tenant), publishes it on the
/// request as an <see cref="IOpenIdContextFeature"/> so endpoints and services can retrieve it, and opens the ambient
/// tenant scope that tenant-scoped data access reads. Installing this once per endpoint group means the authentication
/// (protocol) endpoints and the management endpoints run the exact same environment pipeline.
/// </summary>
[PublicAPI]
public sealed class OpenIdEnvironmentEndpointFilter(
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
            // The factory publishes the context on the request (an IOpenIdContextFeature) for endpoints to retrieve.
            openIdContext = await OpenIdContextFactory.CreateForRequestAsync(
                httpContext,
                mediator,
                httpContext.RequestAborted
            );
        }
        catch (HttpResultException exception)
        {
            // An unresolvable tenant (unknown host/path) is reported as its mapped result (typically 404). The ambient
            // scope opens only below, so tenant/server listings on central-admin surfaces are never tenant-restricted.
            return exception.HttpResult;
        }

        using var scope = AmbientTenantAccessor.BeginScope(openIdContext.Tenant.TenantId);
        return await next(context);
    }
}
