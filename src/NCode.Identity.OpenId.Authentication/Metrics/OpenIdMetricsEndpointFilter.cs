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
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Routing;

namespace NCode.Identity.OpenId.Authentication.Metrics;

/// <summary>
/// An endpoint filter, installed outermost on the OpenID endpoint group, that tags the request's
/// <c>http.server.request.duration</c> measurement with the endpoint's registered name, which stays stable when an
/// operator remaps routes or changes how tenants are routed.
/// </summary>
// See ADR-0197.
internal sealed class OpenIdMetricsEndpointFilter : IEndpointFilter
{
    internal const string EndpointAttribute = "ncode.identity.openid.endpoint";

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
        var tagsFeature = httpContext.Features.Get<IHttpMetricsTagsFeature>();
        if (endpointName is not null && tagsFeature is not null)
            tagsFeature.Tags.Add(
                new KeyValuePair<string, object?>(EndpointAttribute, endpointName)
            );

        return await next(context);
    }
}
