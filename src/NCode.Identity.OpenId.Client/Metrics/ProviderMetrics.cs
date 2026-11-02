// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Metrics;

namespace NCode.Identity.OpenId.Client.Metrics;

/// <summary>
/// Owns the instruments of the <see cref="OpenIdClientMetrics.MeterName"/> meter, created once through the
/// <see cref="IMeterFactory"/> so each service provider has its own; call sites record through its methods and never
/// touch the meter directly.
/// </summary>
// See ADR-1197.
internal sealed class ProviderMetrics
{
    internal const string RetriesName = "ncode.identity.openid_client.retries";

    internal const string TimeoutsName = "ncode.identity.openid_client.timeouts";

    internal const string ErrorTypeAttribute = "error.type";

    internal const string TimeoutScopeAttribute = "ncode.identity.openid_client.timeout_scope";

    internal const string TimeoutErrorType = "timeout";

    // The bounded catch-all value the family's metrics use (OpenIdMetricAttributes.OtherValue), which this package
    // cannot reference.
    internal const string OtherErrorType = "_OTHER";

    internal const string AttemptTimeoutScope = "attempt";

    internal const string TotalTimeoutScope = "total";

    private Counter<long> Retries { get; }

    private Counter<long> Timeouts { get; }

    public ProviderMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(OpenIdClientMetrics.MeterName);

        Retries = meter.CreateCounter<long>(
            RetriesName,
            unit: "{retry}",
            description: "The number of times a call to an OpenID Provider was sent again after a transient failure, by the failure."
        );

        Timeouts = meter.CreateCounter<long>(
            TimeoutsName,
            unit: "{timeout}",
            description: "The number of times an attempt or a whole call to an OpenID Provider timed out, by scope."
        );
    }

    /// <summary>
    /// Records that the client sends a call again after a transient failure.
    /// </summary>
    /// <param name="errorType">The bounded failure: the response's status code, <c>timeout</c>, or the
    /// <see cref="HttpRequestError"/> in snake case.</param>
    public void Retried(string errorType) =>
        Retries.Add(1, new KeyValuePair<string, object?>(ErrorTypeAttribute, errorType));

    /// <summary>
    /// Records that an attempt, or a whole call, of the client timed out.
    /// </summary>
    /// <param name="scope">The bounded scope: <c>attempt</c> or <c>total</c>.</param>
    public void TimedOut(string scope) =>
        Timeouts.Add(1, new KeyValuePair<string, object?>(TimeoutScopeAttribute, scope));
}
