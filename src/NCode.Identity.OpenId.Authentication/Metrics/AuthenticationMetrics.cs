// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;

namespace NCode.Identity.OpenId.Authentication.Metrics;

/// <summary>
/// Owns the instruments of the <see cref="OpenIdAuthenticationMetrics.MeterName"/> meter, created once through the
/// <see cref="IMeterFactory"/> so each service provider has its own; call sites record through its methods and never
/// touch the meter directly.
/// </summary>
// See ADR-1197.
internal sealed class AuthenticationMetrics
{
    internal const string RateLimitingRequestsName = "ncode.identity.rate_limiting.requests";

    internal const string RateLimitingPartitionAttribute = "ncode.identity.rate_limiting.partition";

    internal const string RateLimitingResultAttribute = "ncode.identity.rate_limiting.result";

    internal const string TenantEnvironmentTypeAttribute = "ncode.identity.tenant.environment_type";

    internal const string AdmittedResult = "admitted";

    internal const string RejectedResult = "rejected";

    internal const string OverageResult = "overage";

    internal const string StoreUnavailableResult = "store_unavailable";

    private Counter<long> RateLimitingRequests { get; }

    public AuthenticationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(OpenIdAuthenticationMetrics.MeterName);

        RateLimitingRequests = meter.CreateCounter<long>(
            RateLimitingRequestsName,
            unit: "{request}",
            description: "The number of requests judged against a rate-limit partition, by partition and result."
        );
    }

    /// <summary>
    /// Records that a request was judged against a registered rate-limit descriptor.
    /// </summary>
    /// <param name="partition">The name of the registered rate-limit descriptor.</param>
    /// <param name="result">The bounded result: <c>admitted</c>, <c>overage</c>, <c>rejected</c>, or
    /// <c>store_unavailable</c>.</param>
    /// <param name="environmentType">The tenant's registered environment type, a bounded value.</param>
    public void RateLimitJudged(string partition, string result, string environmentType) =>
        RateLimitingRequests.Add(
            1,
            new KeyValuePair<string, object?>(RateLimitingPartitionAttribute, partition),
            new KeyValuePair<string, object?>(RateLimitingResultAttribute, result),
            new KeyValuePair<string, object?>(TenantEnvironmentTypeAttribute, environmentType)
        );
}
