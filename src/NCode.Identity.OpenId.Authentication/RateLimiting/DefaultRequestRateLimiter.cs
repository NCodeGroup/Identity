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

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Endpoints;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Metrics;
using NCode.Identity.OpenId.Authentication.SharedState;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Provides the default <see cref="IRequestRateLimiter"/>: it judges the limits registered for a stage in one batched
/// call to the shared <see cref="IExpiringCounterSet"/>, so a limit holds across every replica that shares the set. A
/// window starts at a multiple of its length counted from the first tick, so a one-day window starts at midnight UTC.
/// A sliding window estimates the request rate over two fixed windows: a request increments the current window's
/// counter and reads the previous window's, and is refused when <c>previous × (1 − elapsed / window) + current</c>
/// exceeds the limit, and told to retry when the estimate would admit one more request. A fixed window counts exactly
/// and may carry an overage: a request past the limit but within the overage is admitted and adds its overage usage
/// meter, and a request past both is refused until the window ends.
/// A counter is incremented before its limit is judged, so a refused request still counts, and the batch stops at the
/// first limit that refuses. A counter store that throws, or does not answer within <see cref="StoreTimeout"/>, admits
/// the request: the limit protects capacity, so an outage of the store must not refuse every request, nor make every
/// request wait for it. After a failure the store is not called again for <see cref="StoreRetryInterval"/>, so an
/// outage costs one bounded wait per interval per replica rather than one per check; a warning is logged when the store
/// first fails and an information message when it answers again, so an outage is logged once rather than once per
/// request (ADR-0196). Every judged limit is counted in <c>ncode.identity.rate_limiting.requests</c> with its result,
/// so the fail-open path is as visible as a refusal.
/// </summary>
// See ADR-0196, ADR-0198 and ADR-0246.
internal class DefaultRequestRateLimiter(
    IRateLimitDescriptorCatalog catalog,
    IExpiringCounterSet counterSet,
    TimeProvider timeProvider,
    AuthenticationMetrics metrics,
    ILogger<DefaultRequestRateLimiter> logger
) : IRequestRateLimiter
{
    internal const string Purpose = "rate-limit";

    /// <summary>
    /// Gets the longest a counter-store call may take before the request is admitted without it.
    /// </summary>
    internal static TimeSpan StoreTimeout => TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Gets how long the counter store is not called after it fails, before it is tried again.
    /// </summary>
    internal static TimeSpan StoreRetryInterval => TimeSpan.FromSeconds(10);

    private const long BreakerClosed = long.MinValue;

    // The TimeProvider timestamp of the store failure that opened the breaker, or BreakerClosed while the store answers.
    // A monotonic timestamp, so a step of the wall clock cannot hold the breaker open.
    private long _storeFailedTimestamp = BreakerClosed;

    private IRateLimitDescriptorCatalog Catalog { get; } = catalog;

    private IExpiringCounterSet CounterSet { get; } = counterSet;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private AuthenticationMetrics Metrics { get; } = metrics;

    private ILogger<DefaultRequestRateLimiter> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> AcquireAsync(
        string stage,
        RateLimitContext context,
        CancellationToken cancellationToken
    )
    {
        var openIdContext = context.OpenIdContext;
        var httpContext = openIdContext.Http;
        var state = RateLimitRequestState.Get(httpContext);
        var isMetered =
            httpContext.GetEndpoint()?.Metadata.GetMetadata<IOpenIdEndpointMeteredMetadata>()
            is not { IsMetered: false };

        // A client's effective settings merge its tenant's, so a context that names a client reads them instead.
        var settings = context.Client?.Settings ?? openIdContext.Tenant.SettingsProvider.Collection;

        // A descriptor whose limit is unset still adds its usage meter: the tenant's usage is metered without a quota.
        var limits = new List<JudgedRateLimit>();
        var usageMeters = new List<string>();
        foreach (var descriptor in Catalog.GetStage(stage))
        {
            if (descriptor.IsMeteredOnly && !isMetered)
                continue;

            var partitionValue = descriptor.PartitionValueSelector(context);
            if (partitionValue is null)
                continue;

            if (descriptor.UsageMeter is { } usageMeter)
                usageMeters.Add(usageMeter);

            if (!settings.TryGetValue(descriptor.LimitKey, out var limit))
                continue;

            var overage =
                descriptor.OverageKey is { } overageKey
                && settings.TryGetValue(overageKey, out var overageLimit)
                    ? overageLimit
                    : 0;

            limits.Add(new JudgedRateLimit(descriptor, partitionValue, limit, overage));
        }

        if (limits.Count == 0)
            return Admit(state, usageMeters);

        var tenantId = openIdContext.Tenant.TenantId;
        var now = TimeProvider.GetUtcNow();

        if (!IsStoreAvailable(out var breakerState))
            return AdmitUncounted(state, limits, usageMeters);

        IReadOnlyList<ExpiringCounterCount> counts;
        try
        {
            var increments = limits.ConvertAll(judged => CreateIncrement(tenantId, judged, now));
            var operation = CounterSet.IncrementUntilExceededAsync(increments, cancellationToken);
            counts = await WithStoreTimeoutAsync(operation, cancellationToken);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested
            )
        {
            RecordStoreFailure(tenantId, stage, exception);
            return AdmitUncounted(state, limits, usageMeters);
        }

        OnStoreAnswered(breakerState);

        // The store stops after the first limit that refuses, so it answers fewer counts than limits then.
        for (var index = 0; index < counts.Count; index++)
        {
            var judged = limits[index];
            var (result, retryAfter) = Judge(judged, counts[index], now);
            Metrics.RateLimitJudged(judged.Descriptor.Name, result);

            if (
                result == AuthenticationMetrics.OverageResult
                && judged.Descriptor.OverageUsageMeter is { } overageUsageMeter
            )
                usageMeters.Add(overageUsageMeter);

            if (retryAfter is not { } wait)
                continue;

            Logger.RateLimitExceeded(
                tenantId,
                judged.Descriptor.Name,
                judged.Limit,
                judged.Descriptor.Window
            );
            state.IsRefused = true;
            return new RateLimitDecision(wait);
        }

        return Admit(state, usageMeters);
    }

    // A call that has already completed, as every call to the in-process store has, is not given a timer. A call that is
    // abandoned has its eventual failure observed, so it is not reported as an unobserved task exception.
    private async ValueTask<T> WithStoreTimeoutAsync<T>(
        ValueTask<T> operation,
        CancellationToken cancellationToken
    )
    {
        if (operation.IsCompleted)
            return await operation;

        var task = operation.AsTask();
        try
        {
            return await task.WaitAsync(StoreTimeout, TimeProvider, cancellationToken);
        }
        catch when (!task.IsCompleted)
        {
            _ = task.ContinueWith(
                static abandoned => _ = abandoned.Exception,
                CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted
                    | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default
            );
            throw;
        }
    }

    // The breaker state a call begins with is kept, so only an answer to a call made after the breaker opened can close it;
    // an answer to an older call cannot undo a newer failure.
    private bool IsStoreAvailable(out long breakerState)
    {
        breakerState = Interlocked.Read(ref _storeFailedTimestamp);
        return breakerState == BreakerClosed
            || TimeProvider.GetElapsedTime(breakerState) >= StoreRetryInterval;
    }

    // A stage the store could not judge admits the request without counting it, and still records its usage.
    private RateLimitDecision AdmitUncounted(
        RateLimitRequestState state,
        List<JudgedRateLimit> limits,
        List<string> usageMeters
    )
    {
        foreach (var judged in limits)
            Metrics.RateLimitJudged(
                judged.Descriptor.Name,
                AuthenticationMetrics.StoreUnavailableResult
            );

        return Admit(state, usageMeters);
    }

    private static RateLimitDecision Admit(RateLimitRequestState state, List<string> usageMeters)
    {
        // Two registered descriptors, or a stage judged twice, may name one meter, and a request is billed for a
        // meter once.
        foreach (var usageMeter in usageMeters)
        {
            if (!state.UsageMeters.Contains(usageMeter))
                state.UsageMeters.Add(usageMeter);
        }

        return RateLimitDecision.Admitted;
    }

    // Only the failure of a store that was answering logs a warning; a retry that fails again extends the pause quietly,
    // so an outage is logged once per replica rather than once per request.
    private void RecordStoreFailure(string tenantId, string stage, Exception exception)
    {
        var previous = Interlocked.Exchange(ref _storeFailedTimestamp, TimeProvider.GetTimestamp());
        if (previous == BreakerClosed)
            Logger.RateLimitStoreUnavailable(tenantId, stage, StoreRetryInterval, exception);
    }

    // A store that keeps answering costs no write to the shared field.
    private void OnStoreAnswered(long breakerState)
    {
        if (
            breakerState != BreakerClosed
            && Interlocked.CompareExchange(ref _storeFailedTimestamp, BreakerClosed, breakerState)
                == breakerState
        )
            Logger.RateLimitStoreRecovered();
    }

    // The earliest time one more request would be admitted, assuming no other request arrives meanwhile. Within the
    // current window the previous window's weight decays until previous × (1 − e / W) + current + 1 ≤ limit; when the
    // current window alone is at the limit, its count becomes the next window's previous and decays the same way.
    private static DateTimeOffset GetRetryAt(
        DateTimeOffset windowStart,
        TimeSpan window,
        long current,
        long previous,
        int limit
    )
    {
        var room = limit - current - 1;
        if (room >= 0 && previous > 0)
            return windowStart + window * (1.0 - (double)room / previous);

        var nextWindowStart = windowStart + window;
        var decay = 1.0 - (double)(limit - 1) / current;
        return decay > 0 ? nextWindowStart + window * decay : nextWindowStart;
    }

    private static DateTimeOffset GetWindowStart(DateTimeOffset now, TimeSpan window) =>
        new(now.UtcTicks - now.UtcTicks % window.Ticks, TimeSpan.Zero);

    // A fixed window counts its own counter, bounded by the limit and the overage. A sliding window keeps its counter
    // for two windows so the next window can read it as its previous, and weighs the previous window's counter by how
    // much of it the sliding window still covers.
    private static ExpiringCounterIncrement CreateIncrement(
        string tenantId,
        JudgedRateLimit judged,
        DateTimeOffset now
    )
    {
        var descriptor = judged.Descriptor;
        var window = descriptor.Window;
        var windowStart = GetWindowStart(now, window);
        var key = CreateKey(tenantId, descriptor.Name, judged.PartitionValue, windowStart);

        if (!descriptor.IsSliding)
        {
            return new ExpiringCounterIncrement
            {
                Key = key,
                ExpiresWhen = windowStart + window,
                Limit = (long)judged.Limit + judged.Overage,
            };
        }

        var previousKey = CreateKey(
            tenantId,
            descriptor.Name,
            judged.PartitionValue,
            windowStart - window
        );
        return new ExpiringCounterIncrement
        {
            Key = key,
            ExpiresWhen = windowStart + window + window,
            Limit = judged.Limit,
            ReferenceKey = previousKey,
            ReferenceWeight = 1.0 - (now - windowStart) / window,
        };
    }

    private static (string Result, TimeSpan? RetryAfter) Judge(
        JudgedRateLimit judged,
        ExpiringCounterCount counted,
        DateTimeOffset now
    )
    {
        var descriptor = judged.Descriptor;
        var window = descriptor.Window;
        var windowStart = GetWindowStart(now, window);

        if (descriptor.IsSliding)
        {
            var previousWeight = 1.0 - (now - windowStart) / window;
            var estimate = counted.ReferenceCount * previousWeight + counted.Count;
            if (estimate <= judged.Limit)
                return (AuthenticationMetrics.AdmittedResult, null);

            var retryAt = GetRetryAt(
                windowStart,
                window,
                counted.Count,
                counted.ReferenceCount,
                judged.Limit
            );
            return (AuthenticationMetrics.RejectedResult, retryAt - now);
        }

        if (counted.Count <= judged.Limit)
            return (AuthenticationMetrics.AdmittedResult, null);

        if (counted.Count <= (long)judged.Limit + judged.Overage)
            return (AuthenticationMetrics.OverageResult, null);

        return (AuthenticationMetrics.RejectedResult, windowStart + window - now);
    }

    private static string CreateKey(
        string tenantId,
        string partition,
        string value,
        DateTimeOffset windowStart
    ) =>
        ExpiringKeys.Create(
            Purpose,
            tenantId,
            partition,
            value,
            windowStart.UtcTicks.ToString(CultureInfo.InvariantCulture)
        );
}
