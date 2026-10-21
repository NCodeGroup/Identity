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
/// An endpoint filter, installed once on the OpenID endpoint group inside the environment filter, that counts every
/// request against its tenant's tenant-wide and per-address rate limits and answers a refused request with
/// <c>429 Too Many Requests</c> before its endpoint runs. It sits outside the exception filter, so a refused request is
/// not audited as a protocol error: an attacker flooding the endpoint must not flood the audit log as well.
/// </summary>
// See ADR-0196; a request every limit admits is the tenant's usage (ADR-0199).
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
        var decision = await RateLimiter.AcquireForTenantAsync(
            openIdContext,
            httpContext.RequestAborted
        );
        if (decision.RetryAfter is { } retryAfter)
            return RateLimitErrors.Create(openIdContext, retryAfter).AsHttpResult();

        UsageRecorder.Record(openIdContext.Tenant.TenantId, TenantUsageMeters.Requests);
        return await next(context);
    }
}
