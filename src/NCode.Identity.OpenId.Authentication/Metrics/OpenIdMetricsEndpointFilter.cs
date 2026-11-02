// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Metrics;

namespace NCode.Identity.OpenId.Authentication.Metrics;

/// <summary>
/// An endpoint filter, installed outermost on the OpenID endpoint group, that tags the request's
/// <c>http.server.request.duration</c> measurement with the endpoint's registered name, which stays stable when an
/// operator remaps routes or changes how tenants are routed.
/// </summary>
// See ADR-1197.
internal sealed class OpenIdMetricsEndpointFilter : IEndpointFilter
{
    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var httpContext = context.HttpContext;
        var endpointName = httpContext
            .GetEndpoint()
            ?.Metadata.GetMetadata<IEndpointNameMetadata>()
            ?.EndpointName;
        if (endpointName is not null)
            OpenIdMetricAttributes.Add(httpContext, OpenIdMetricAttributes.Endpoint, endpointName);

        return await next(context);
    }
}
