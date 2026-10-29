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

using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace NCode.Identity.Bff.Diagnostics;

/// <summary>
/// Owns the instruments of the <see cref="BffTelemetry.MeterName"/> meter, created once through the
/// <see cref="IMeterFactory"/> so each service provider has its own; call sites record through its methods and never
/// touch the meter directly. Logins, session ends and rejections are audit events, which the event seam counts; these
/// instruments measure the BFF's own mechanisms.
/// </summary>
// See ADR-0197.
internal sealed class BffMetrics
{
    internal const string TokenRefreshDurationName = "ncode.identity.bff.token.refresh.duration";

    internal const string SessionStoreOperationDurationName =
        "ncode.identity.bff.session_store.operation.duration";

    internal const string RefreshResultAttribute = "ncode.identity.bff.refresh.result";

    internal const string SessionStoreOperationAttribute =
        "ncode.identity.bff.session_store.operation";

    internal const string ErrorTypeAttribute = "error.type";

    /// <summary>
    /// The refresh result when this request presented the refresh token and stored the new token.
    /// </summary>
    internal const string RefreshedResult = "refreshed";

    /// <summary>
    /// The refresh result when another request or replica refreshed the session and this request used its token.
    /// </summary>
    internal const string JoinedResult = "joined";

    /// <summary>
    /// The refresh result when the request got no token; <c>error.type</c> names the failure reason.
    /// </summary>
    internal const string FailedResult = "failed";

    internal const string CreateOperation = "create";
    internal const string GetOperation = "get";
    internal const string ReplaceOperation = "replace";
    internal const string RemoveOperation = "remove";
    internal const string FindBySessionIdOperation = "find_by_session_id";
    internal const string FindBySubjectOperation = "find_by_subject";
    internal const string AddTransactionOperation = "add_transaction";
    internal const string TakeTransactionOperation = "take_transaction";
    internal const string AcquireRefreshLeaseOperation = "acquire_refresh_lease";
    internal const string IsRefreshLeaseHeldOperation = "is_refresh_lease_held";
    internal const string ReleaseRefreshLeaseOperation = "release_refresh_lease";

    // A refresh is a token endpoint round trip, or a wait of up to the refresh lease (40 s by default) for another
    // replica's, so the buckets run from milliseconds to the lease.
    private static IReadOnlyList<double> RefreshBucketBoundaries { get; } =
    [0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5, 5, 10, 20, 40];

    // A store operation is one or a few round trips to a nearby server, so the buckets resolve the sub-millisecond
    // range and still bound a stalled call.
    private static IReadOnlyList<double> StoreBucketBoundaries { get; } =
    [0.0001, 0.00025, 0.0005, 0.001, 0.0025, 0.005, 0.01, 0.025, 0.05, 0.1, 0.25, 0.5, 1, 2.5];

    private Histogram<double> TokenRefreshDuration { get; }

    private Histogram<double> SessionStoreOperationDuration { get; }

    public BffMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create(BffTelemetry.MeterName);

        TokenRefreshDuration = meter.CreateHistogram(
            TokenRefreshDurationName,
            unit: "s",
            description: "The time a request spent getting a refreshed user access token, waits included.",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = RefreshBucketBoundaries,
            }
        );

        SessionStoreOperationDuration = meter.CreateHistogram(
            SessionStoreOperationDurationName,
            unit: "s",
            description: "The duration of a BFF session store operation.",
            advice: new InstrumentAdvice<double>
            {
                HistogramBucketBoundaries = StoreBucketBoundaries,
            }
        );
    }

    /// <summary>
    /// Records a user access-token refresh that started at <paramref name="startTimestamp"/>.
    /// </summary>
    /// <param name="startTimestamp">The <see cref="Stopwatch.GetTimestamp"/> at which the refresh started.</param>
    /// <param name="result">The bounded result: <see cref="RefreshedResult"/>, <see cref="JoinedResult"/> or
    /// <see cref="FailedResult"/>.</param>
    /// <param name="failureReason">The failure reason, a <c>BffTokenFailureReasons</c> value, when it failed.</param>
    public void TokenRefreshCompleted(long startTimestamp, string result, string? failureReason)
    {
        if (!TokenRefreshDuration.Enabled)
            return;

        var tags = new TagList { { RefreshResultAttribute, result } };
        if (failureReason is not null)
            tags.Add(ErrorTypeAttribute, failureReason);

        TokenRefreshDuration.Record(Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds, tags);
    }

    /// <summary>
    /// Records a session store operation that started at <paramref name="startTimestamp"/>.
    /// </summary>
    /// <param name="operation">The bounded operation name, such as <see cref="GetOperation"/>.</param>
    /// <param name="startTimestamp">The <see cref="Stopwatch.GetTimestamp"/> at which the operation started.</param>
    /// <param name="exception">The exception the operation threw, or <see langword="null"/> when it succeeded.</param>
    public void SessionStoreOperationCompleted(
        string operation,
        long startTimestamp,
        Exception? exception
    )
    {
        if (!SessionStoreOperationDuration.Enabled)
            return;

        var tags = new TagList { { SessionStoreOperationAttribute, operation } };
        if (exception is not null)
            tags.Add(ErrorTypeAttribute, exception.GetType().FullName);

        SessionStoreOperationDuration.Record(
            Stopwatch.GetElapsedTime(startTimestamp).TotalSeconds,
            tags
        );
    }
}
