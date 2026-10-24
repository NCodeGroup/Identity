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
using Microsoft.AspNetCore.Routing;
using NCode.Identity.OpenId.Metrics;

namespace NCode.Identity.OpenId.Authentication.Metrics;

/// <summary>
/// An endpoint filter, installed outermost on the OpenID endpoint group, that tags the request's
/// <c>http.server.request.duration</c> measurement with the endpoint's registered name, which stays stable when an
/// operator remaps routes or changes how tenants are routed.
/// </summary>
// See ADR-0197.
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
