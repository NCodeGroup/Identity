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
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Clients;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.SharedState;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Authentication.RateLimiting;

/// <summary>
/// Provides the default <see cref="IRequestRateLimiter"/>: a sliding-window estimate over two one-minute fixed windows
/// counted in the shared <see cref="IExpiringCounterSet"/>, so a limit holds across every replica that shares the set.
/// A request increments the current window's counter and reads the previous window's; it is refused when
/// <c>previous × (1 − elapsed / window) + current</c> exceeds the limit, and told to retry when the current window ends.
/// The counter is incremented before the limit is judged, so a refused request still counts. A counter store that
/// throws admits the request and logs a warning: the limit protects capacity, so an outage of the store must not refuse
/// every request.
/// </summary>
internal class DefaultRequestRateLimiter(
    IExpiringCounterSet counterSet,
    TimeProvider timeProvider,
    ILogger<DefaultRequestRateLimiter> logger
) : IRequestRateLimiter
{
    internal const string Purpose = "rate-limit";

    internal const string TenantPartition = "tenant";

    internal const string AddressPartition = "address";

    internal const string ClientPartition = "client";

    internal static TimeSpan Window => TimeSpan.FromMinutes(1);

    private IExpiringCounterSet CounterSet { get; } = counterSet;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<DefaultRequestRateLimiter> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> AcquireForTenantAsync(
        OpenIdContext openIdContext,
        CancellationToken cancellationToken
    )
    {
        var tenantId = openIdContext.Tenant.TenantId;
        var settings = openIdContext.Tenant.SettingsProvider.Collection;

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

        var remoteAddress = openIdContext.Http.Connection.RemoteIpAddress;
        if (
            remoteAddress is not null
            && settings.TryGetValue(
                OpenIdSettingKeys.RateLimitAddressPerMinute,
                out var addressLimit
            )
        )
        {
            return await AcquireAsync(
                tenantId,
                AddressPartition,
                remoteAddress.ToString(),
                addressLimit,
                cancellationToken
            );
        }

        return RateLimitDecision.Admitted;
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

        long current;
        long previous;
        try
        {
            // The current window's counter is kept for two windows so the next window can read it as its previous.
            current = await CounterSet.IncrementAsync(
                CreateKey(tenantId, partition, value, windowStart),
                windowStart + Window + Window,
                cancellationToken
            );
            previous = await CounterSet.GetAsync(
                CreateKey(tenantId, partition, value, previousWindowStart),
                cancellationToken
            );
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException
                || !cancellationToken.IsCancellationRequested
            )
        {
            Logger.RateLimitStoreUnavailable(tenantId, partition, exception);
            return RateLimitDecision.Admitted;
        }

        var elapsed = now - windowStart;
        var previousWeight = 1.0 - elapsed / Window;
        var estimate = previous * previousWeight + current;
        if (estimate <= limit)
            return RateLimitDecision.Admitted;

        Logger.RateLimitExceeded(tenantId, partition, limit);
        return new RateLimitDecision(windowStart + Window - now);
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
