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

    internal const string OutboundRetriesName = "ncode.identity.outbound_http.retries";

    internal const string OutboundTimeoutsName = "ncode.identity.outbound_http.timeouts";

    internal const string ErrorTypeAttribute = "error.type";

    internal const string TimeoutScopeAttribute = "ncode.identity.outbound_http.timeout_scope";

    internal const string TimeoutErrorType = "timeout";

    internal const string AttemptTimeoutScope = "attempt";

    internal const string TotalTimeoutScope = "total";

    private Counter<long> RateLimitingRequests { get; }

    private Counter<long> OutboundRetries { get; }

    private Counter<long> OutboundTimeouts { get; }

    public AuthenticationMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(OpenIdAuthenticationMetrics.MeterName);

        RateLimitingRequests = meter.CreateCounter<long>(
            RateLimitingRequestsName,
            unit: "{request}",
            description: "The number of requests judged against a rate-limit partition, by partition and result."
        );

        OutboundRetries = meter.CreateCounter<long>(
            OutboundRetriesName,
            unit: "{retry}",
            description: "The number of times a call to a caller-supplied URL was sent again after a transient failure, by the failure."
        );

        OutboundTimeouts = meter.CreateCounter<long>(
            OutboundTimeoutsName,
            unit: "{timeout}",
            description: "The number of times an attempt or a whole call to a caller-supplied URL timed out, by scope."
        );
    }

    /// <summary>
    /// Records that the guarded outbound client sends a call again after a transient failure.
    /// </summary>
    /// <param name="errorType">The bounded failure: the response's status code, <c>timeout</c>, or the
    /// <see cref="HttpRequestError"/> in snake case.</param>
    public void OutboundRetried(string errorType) =>
        OutboundRetries.Add(1, new KeyValuePair<string, object?>(ErrorTypeAttribute, errorType));

    /// <summary>
    /// Records that an attempt, or a whole call, of the guarded outbound client timed out.
    /// </summary>
    /// <param name="scope">The bounded scope: <c>attempt</c> or <c>total</c>.</param>
    public void OutboundTimedOut(string scope) =>
        OutboundTimeouts.Add(1, new KeyValuePair<string, object?>(TimeoutScopeAttribute, scope));

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
