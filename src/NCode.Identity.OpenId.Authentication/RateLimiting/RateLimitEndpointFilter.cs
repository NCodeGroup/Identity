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
using NCode.Identity.OpenId.Authentication.Usage;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Results;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// An endpoint filter, installed once on the OpenID endpoint group inside the environment filter, that judges the
/// <c>request</c> rate-limit stage of every request and answers a refused request with <c>429 Too Many Requests</c>
/// before its endpoint runs. The rate-limit descriptors registered for each stage, not the filter, decide which limits
/// and quotas apply to a request and which usage meters it adds. The filter sits outside the exception filter, so a
/// refused request is not audited as a protocol error: an attacker flooding the endpoint must not flood the audit log
/// as well. Once the endpoint has run, the usage meters that the stages admitting the request added are recorded as the
/// tenant's usage, unless a stage refused the request, here or inside the endpoint.
/// </summary>
// See ADR-0196, ADR-0198 and ADR-0246; a request every stage admits is the tenant's usage (ADR-0199).
internal sealed class RateLimitEndpointFilter(
    IRequestRateLimiter rateLimiter,
    ITenantUsageRecorder usageRecorder
) : IEndpointFilter
{
    private IRequestRateLimiter RateLimiter { get; } = rateLimiter;

    private ITenantUsageRecorder UsageRecorder { get; } = usageRecorder;

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context,
        EndpointFilterDelegate next
    )
    {
        var httpContext = context.HttpContext;

        // The environment filter publishes the context; without it the request never entered the OpenID pipeline.
        var feature = httpContext.Features.Get<IOpenIdContextFeature>();
        if (feature is null)
            return await next(context);

        var openIdContext = feature.OpenIdContext;
        var rateLimitContext = new RateLimitContext { OpenIdContext = openIdContext };
        var decision = await RateLimiter.AcquireAsync(
            RateLimitStages.Request,
            rateLimitContext,
            httpContext.RequestAborted
        );
        if (decision.RetryAfter is { } retryAfter)
            return RateLimitErrors.Refuse(openIdContext, retryAfter).AsHttpResult();

        var result = await next(context);

        var state = RateLimitRequestState.Get(httpContext);
        if (state.IsRefused)
            return result;

        var tenantId = openIdContext.Tenant.TenantId;
        foreach (var meter in state.UsageMeters)
            UsageRecorder.Record(tenantId, meter);

        return result;
    }
}
