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
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.Metrics;
using NCode.Identity.OpenId.Authentication.SharedState;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.Settings;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Provides the default <see cref="IRequestRateLimiter"/>: a sliding-window estimate over two one-minute fixed windows
/// counted in the shared <see cref="IExpiringCounterSet"/>, so a limit holds across every replica that shares the set.
/// A request increments the current window's counter and reads the previous window's; it is refused when
/// <c>previous × (1 − elapsed / window) + current</c> exceeds the limit, and told to retry when the estimate would admit
/// one more request.
/// The counter is incremented before the limit is judged, so a refused request still counts. A counter store that
/// throws, or does not answer within <see cref="StoreTimeout"/>, admits the request: the limit protects capacity, so an
/// outage of the store must not refuse every request, nor make every request wait for it. After a failure the store is
/// not called again for <see cref="StoreRetryInterval"/>, so an outage costs one bounded wait per interval per replica
/// rather than one per check; a warning is logged when the store first fails and an information message when it answers
/// again, so an outage is logged once rather than once per request (ADR-0196). Every judged partition is counted in
/// <c>ncode.identity.rate_limiting.requests</c> with its result, so the fail-open path is as visible as a refusal.
/// </summary>
internal class DefaultRequestRateLimiter(
    IExpiringCounterSet counterSet,
    TimeProvider timeProvider,
    AuthenticationMetrics metrics,
    ILogger<DefaultRequestRateLimiter> logger
) : IRequestRateLimiter
{
    internal const string Purpose = "rate-limit";

    internal const string TenantPartition = "tenant";

    internal const string AddressPartition = "address";

    internal const string ClientPartition = "client";

    internal const string DailyRequestsPartition = "daily_requests";

    internal const string DailyTokenRequestsPartition = "daily_token_requests";

    internal static TimeSpan Window => TimeSpan.FromMinutes(1);

    internal static TimeSpan QuotaWindow => TimeSpan.FromDays(1);

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

    private IExpiringCounterSet CounterSet { get; } = counterSet;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private AuthenticationMetrics Metrics { get; } = metrics;

    private ILogger<DefaultRequestRateLimiter> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> AcquireForTenantAsync(
        OpenIdContext openIdContext,
        bool isMetered,
        CancellationToken cancellationToken
    )
    {
        var tenantId = openIdContext.Tenant.TenantId;
        var settings = openIdContext.Tenant.SettingsProvider.Collection;

        // The narrower address limit is judged first, and a request it refuses is not counted against the tenant, so
        // one address over its own limit cannot spend the allowance every other caller of the tenant shares.
        var remoteAddress = openIdContext.Http.Connection.RemoteIpAddress;
        if (
            remoteAddress is not null
            && settings.TryGetValue(
                OpenIdSettingKeys.RateLimitAddressPerMinute,
                out var addressLimit
            )
        )
        {
            var decision = await AcquireAsync(
                tenantId,
                AddressPartition,
                GetAddressPartitionValue(remoteAddress),
                addressLimit,
                cancellationToken
            );
            if (decision.IsRejected)
                return decision;
        }

        if (settings.TryGetValue(OpenIdSettingKeys.RateLimitTenantPerMinute, out var tenantLimit))
        {
            var decision = await AcquireAsync(
                tenantId,
                TenantPartition,
                string.Empty,
                tenantLimit,
                cancellationToken
            );
            if (decision.IsRejected)
                return decision;
        }

        // The daily quota is judged last, so a request a per-minute limit refuses does not spend the tenant's quota. A
        // request that is not metered, such as one for public metadata, never spends it (ADR-0198).
        if (
            isMetered
            && settings.TryGetValue(OpenIdSettingKeys.QuotaTenantRequestsPerDay, out var quota)
        )
        {
            return await AcquireQuotaAsync(
                tenantId,
                DailyRequestsPartition,
                quota,
                settings,
                OpenIdSettingKeys.QuotaTenantRequestsOveragePerDay,
                cancellationToken
            );
        }

        return RateLimitDecision.Admitted;
    }

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> AcquireForTokenRequestAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    )
    {
        var settings = openIdContext.Tenant.SettingsProvider.Collection;
        if (!settings.TryGetValue(OpenIdSettingKeys.QuotaTenantTokenRequestsPerDay, out var quota))
            return RateLimitDecision.Admitted;

        return await AcquireQuotaAsync(
            openIdContext.Tenant.TenantId,
            DailyTokenRequestsPartition,
            quota,
            settings,
            OpenIdSettingKeys.QuotaTenantTokenRequestsOveragePerDay,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> AcquireForClientAsync(
        OpenIdContext openIdContext,
        OpenIdClient client,
        CancellationToken cancellationToken
    )
    {
        if (!client.Settings.TryGetValue(OpenIdSettingKeys.RateLimitClientPerMinute, out var limit))
            return RateLimitDecision.Admitted;

        return await AcquireAsync(
            openIdContext.Tenant.TenantId,
            ClientPartition,
            client.ClientId,
            limit,
            cancellationToken
        );
    }

    private async ValueTask<RateLimitDecision> AcquireAsync(
        string tenantId,
        string partition,
        string value,
        int limit,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var windowTicks = Window.Ticks;
        var windowStart = new DateTimeOffset(
            now.UtcTicks - now.UtcTicks % windowTicks,
            TimeSpan.Zero
        );
        var previousWindowStart = windowStart - Window;

        if (!IsStoreAvailable(out var breakerState))
            return AdmitWithoutStore(partition);

        long current;
        long previous;
        try
        {
            // The current window's counter is kept for two windows so the next window can read it as its previous.
            current = await WithStoreTimeoutAsync(
                CounterSet.IncrementAsync(
                    CreateKey(tenantId, partition, value, windowStart),
                    windowStart + Window + Window,
                    cancellationToken
                ),
                cancellationToken
            );
            previous = await WithStoreTimeoutAsync(
                CounterSet.GetAsync(
                    CreateKey(tenantId, partition, value, previousWindowStart),
                    cancellationToken
                ),
                cancellationToken
            );
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested
            )
        {
            return OnStoreFailed(tenantId, partition, exception);
        }

        OnStoreAnswered(breakerState);

        var elapsed = now - windowStart;
        var previousWeight = 1.0 - elapsed / Window;
        var estimate = previous * previousWeight + current;
        if (estimate <= limit)
        {
            Metrics.RateLimitJudged(partition, AuthenticationMetrics.AdmittedResult);
            return RateLimitDecision.Admitted;
        }

        Logger.RateLimitExceeded(tenantId, partition, limit);
        Metrics.RateLimitJudged(partition, AuthenticationMetrics.RejectedResult);
        return new RateLimitDecision(GetRetryAt(windowStart, current, previous, limit) - now);
    }

    // A quota counts one fixed window per UTC calendar day, so it resets at midnight UTC for every tenant and a refused
    // caller is told to retry then. The count is incremented before it is judged, as the per-minute limits are. A tier
    // that sets an overage admits that many more requests past the quota as billable overage, and refuses past both, so
    // its bill stays bounded (ADR-0200).
    private async ValueTask<RateLimitDecision> AcquireQuotaAsync(
        string tenantId,
        string partition,
        int quota,
        IReadOnlySettingCollection settings,
        SettingKey<int> overageKey,
        CancellationToken cancellationToken
    )
    {
        var now = TimeProvider.GetUtcNow();
        var dayStart = new DateTimeOffset(now.UtcDateTime.Date, TimeSpan.Zero);
        var dayEnd = dayStart + QuotaWindow;

        if (!IsStoreAvailable(out var breakerState))
            return AdmitWithoutStore(partition);

        long count;
        try
        {
            count = await WithStoreTimeoutAsync(
                CounterSet.IncrementAsync(
                    CreateKey(tenantId, partition, string.Empty, dayStart),
                    dayEnd,
                    cancellationToken
                ),
                cancellationToken
            );
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested
            )
        {
            return OnStoreFailed(tenantId, partition, exception);
        }

        OnStoreAnswered(breakerState);

        if (count <= quota)
        {
            Metrics.RateLimitJudged(partition, AuthenticationMetrics.AdmittedResult);
            return RateLimitDecision.Admitted;
        }

        if (settings.TryGetValue(overageKey, out var overage) && count <= (long)quota + overage)
        {
            Metrics.RateLimitJudged(partition, AuthenticationMetrics.OverageResult);
            return RateLimitDecision.Overage;
        }

        Logger.QuotaExceeded(tenantId, partition, quota);
        Metrics.RateLimitJudged(partition, AuthenticationMetrics.RejectedResult);
        return new RateLimitDecision(dayEnd - now);
    }

    // A call that has already completed, as every call to the in-process store has, is not given a timer. A call that is
    // abandoned has its eventual failure observed, so it is not reported as an unobserved task exception.
    private async ValueTask<long> WithStoreTimeoutAsync(
        ValueTask<long> operation,
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

    private RateLimitDecision AdmitWithoutStore(string partition)
    {
        Metrics.RateLimitJudged(partition, AuthenticationMetrics.StoreUnavailableResult);
        return RateLimitDecision.Admitted;
    }

    // Only the failure of a store that was answering logs a warning; a retry that fails again extends the pause quietly,
    // so an outage is logged once per replica rather than once per request.
    private RateLimitDecision OnStoreFailed(string tenantId, string partition, Exception exception)
    {
        var previous = Interlocked.Exchange(ref _storeFailedTimestamp, TimeProvider.GetTimestamp());
        if (previous == BreakerClosed)
            Logger.RateLimitStoreUnavailable(tenantId, partition, StoreRetryInterval, exception);

        return AdmitWithoutStore(partition);
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
        long current,
        long previous,
        int limit
    )
    {
        var room = limit - current - 1;
        if (room >= 0 && previous > 0)
            return windowStart + Window * (1.0 - (double)room / previous);

        var nextWindowStart = windowStart + Window;
        var decay = 1.0 - (double)(limit - 1) / current;
        return decay > 0 ? nextWindowStart + Window * decay : nextWindowStart;
    }

    // An IPv6 host is usually assigned at least a /64, so one caller could otherwise spread its requests across many
    // addresses; an IPv4-mapped address is counted as the IPv4 address it maps.
    private static string GetAddressPartitionValue(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.AddressFamily != AddressFamily.InterNetworkV6)
            return address.ToString();

        Span<byte> bytes = stackalloc byte[16];
        address.TryWriteBytes(bytes, out _);
        bytes[8..].Clear();
        return new IPAddress(bytes).ToString() + "/64";
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
