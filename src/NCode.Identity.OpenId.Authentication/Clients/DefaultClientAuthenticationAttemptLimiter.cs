// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Authentication.RateLimiting;
using NCode.Identity.OpenId.Authentication.SharedState;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Settings;
using NCode.Identity.SharedState;

namespace NCode.Identity.OpenId.Authentication.Clients;

/// <summary>
/// Provides the default <see cref="IClientAuthenticationAttemptLimiter"/>. It counts failed client authentications in
/// the shared <see cref="IExpiringCounterSet"/> in fixed windows of <c>client_authentication_failed_attempts_window</c>
/// that start at multiples of the window's length, in two partitions: the request's remote address, limited by
/// <c>client_authentication_failed_attempts_address_limit</c>, and the remote address with the presented
/// <c>client_id</c>, limited by <c>client_authentication_failed_attempts_limit</c>. A partition at its limit refuses
/// every client authentication it covers until its window ends. A request without a remote address is not limited,
/// and the first such request logs a warning, so a host whose proxy does not forward the client's address learns that
/// the limit is off. A counter store that throws propagates the exception, so the limit fails closed: brute-force
/// protection is a correctness guard, unlike the request rate limiter, a capacity guard, which fails open.
/// </summary>
/// <remarks>
/// Every failure increments both counters of its window together, so the count of an address and a <c>client_id</c>
/// never exceeds the count of the address. A check therefore reads the address's counter first, and reads the counter of
/// the address and the <c>client_id</c> only when the address has at least as many failures as that limit, so a check
/// from an address without failures costs one read. A check's reads are kept for the rest of the request, so the
/// handlers that each examine a request share them.
/// </remarks>
// See ADR-1399.
internal sealed class DefaultClientAuthenticationAttemptLimiter(
    IExpiringCounterSet counterSet,
    TimeProvider timeProvider,
    ILogger<DefaultClientAuthenticationAttemptLimiter> logger
) : IClientAuthenticationAttemptLimiter
{
    internal const string Purpose = "client-authentication-attempts";

    /// <summary>
    /// The partition of the failures from one remote address, for any <c>client_id</c>.
    /// </summary>
    internal const string AddressPartition = "address";

    /// <summary>
    /// The partition of the failures from one remote address for one <c>client_id</c>.
    /// </summary>
    internal const string AddressClientPartition = "address_client";

    private static object ItemKey { get; } = new();

    // Set once the warning about a request without a remote address is logged, so a flood of such requests logs one.
    private int _warnedWithoutRemoteAddress;

    private IExpiringCounterSet CounterSet { get; } = counterSet;

    private TimeProvider TimeProvider { get; } = timeProvider;

    private ILogger<DefaultClientAuthenticationAttemptLimiter> Logger { get; } = logger;

    /// <inheritdoc />
    public async ValueTask<RateLimitDecision> CheckAsync(
        OpenIdContext openIdContext,
        string? clientId,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetScope(openIdContext, out var scope))
            return RateLimitDecision.Admitted;

        var state = GetState(openIdContext.Http, scope.WindowStart);

        if (state.AddressCount is not { } addressCount)
        {
            var addressKey = CreateAddressKey(
                scope.TenantId,
                scope.AddressValue,
                scope.WindowStart
            );
            addressCount = await CounterSet.GetAsync(addressKey, cancellationToken);
            state.AddressCount = addressCount;
        }

        if (addressCount >= scope.AddressLimit)
            return Refuse(openIdContext, AddressPartition, scope.AddressLimit, scope);

        if (clientId is null || addressCount < scope.ClientLimit)
            return RateLimitDecision.Admitted;

        if (!state.ClientCounts.TryGetValue(clientId, out var clientCount))
        {
            var clientKey = CreateAddressClientKey(
                scope.TenantId,
                scope.AddressValue,
                clientId,
                scope.WindowStart
            );
            clientCount = await CounterSet.GetAsync(clientKey, cancellationToken);
            state.ClientCounts[clientId] = clientCount;
        }

        if (clientCount >= scope.ClientLimit)
            return Refuse(openIdContext, AddressClientPartition, scope.ClientLimit, scope);

        return RateLimitDecision.Admitted;
    }

    /// <inheritdoc />
    public async ValueTask RecordFailureAsync(
        OpenIdContext openIdContext,
        string? clientId,
        CancellationToken cancellationToken
    )
    {
        if (!TryGetScope(openIdContext, out var scope))
            return;

        var windowEnd = scope.WindowStart + scope.Window;

        // One batch, one round trip: the address's counter first, then the counter of the address and the client_id,
        // which the batch skips once the address is past its limit, so it never counts more than the address.
        List<ExpiringCounterIncrement> increments =
        [
            new()
            {
                Key = CreateAddressKey(scope.TenantId, scope.AddressValue, scope.WindowStart),
                ExpiresWhen = windowEnd,
                Limit = scope.AddressLimit,
            },
        ];
        if (clientId is not null)
            increments.Add(
                new ExpiringCounterIncrement
                {
                    Key = CreateAddressClientKey(
                        scope.TenantId,
                        scope.AddressValue,
                        clientId,
                        scope.WindowStart
                    ),
                    ExpiresWhen = windowEnd,
                    Limit = scope.ClientLimit,
                }
            );

        var counts = await CounterSet.IncrementUntilExceededAsync(increments, cancellationToken);

        // The request goes on with the counts it just made, should another handler ask again.
        var state = GetState(openIdContext.Http, scope.WindowStart);
        if (counts.Count > 0)
            state.AddressCount = counts[0].Count;
        if (clientId is not null && counts.Count > 1)
            state.ClientCounts[clientId] = counts[1].Count;
    }

    // A check and a count read the request's address, its tenant's limits and the window the same way, so the keys a
    // check reads are the keys a failure writes. A request without a remote address is not limited.
    private bool TryGetScope(OpenIdContext openIdContext, out AttemptScope scope)
    {
        if (openIdContext.Http.Connection.RemoteIpAddress is not { } address)
        {
            WarnWithoutRemoteAddress(openIdContext);
            scope = default;
            return false;
        }

        var tenant = openIdContext.Tenant;
        var settings = tenant.SettingsProvider.Collection;
        var window = settings.GetValue(OpenIdSettingKeys.ClientAuthenticationFailedAttemptsWindow);
        var now = TimeProvider.GetUtcNow();

        scope = new AttemptScope(
            tenant.TenantId,
            RemoteAddressPartition.GetValue(address),
            settings.GetValue(OpenIdSettingKeys.ClientAuthenticationFailedAttemptsAddressLimit),
            settings.GetValue(OpenIdSettingKeys.ClientAuthenticationFailedAttemptsLimit),
            window,
            GetWindowStart(now, window),
            now
        );
        return true;
    }

    // The limit is off for every request of a host that sees no remote address, so the operator is told once per
    // process, never per request.
    private void WarnWithoutRemoteAddress(OpenIdContext openIdContext)
    {
        if (Interlocked.Exchange(ref _warnedWithoutRemoteAddress, 1) == 0)
            Logger.ClientAuthenticationAttemptLimitSkippedWithoutRemoteAddress(
                openIdContext.Tenant.TenantId
            );
    }

    private RateLimitDecision Refuse(
        OpenIdContext openIdContext,
        string partition,
        int limit,
        in AttemptScope scope
    )
    {
        Logger.ClientAuthenticationAttemptLimitReached(
            scope.TenantId,
            partition,
            limit,
            scope.Window
        );

        // A refused request is not billed as the tenant's usage, as a request a rate limit refuses is not.
        RateLimitRequestState.Get(openIdContext.Http).IsRefused = true;

        return new RateLimitDecision(scope.WindowStart + scope.Window - scope.Now);
    }

    // The reads of a request hold for the window they were made in; a request that spans a window's end reads again.
    private static ClientAuthenticationAttemptState GetState(
        HttpContext httpContext,
        DateTimeOffset windowStart
    )
    {
        if (
            httpContext.Items.TryGetValue(ItemKey, out var stored)
            && stored is ClientAuthenticationAttemptState existing
            && existing.WindowStart == windowStart
        )
            return existing;

        var state = new ClientAuthenticationAttemptState(windowStart);
        httpContext.Items[ItemKey] = state;
        return state;
    }

    private static DateTimeOffset GetWindowStart(DateTimeOffset now, TimeSpan window) =>
        new(now.UtcTicks - now.UtcTicks % window.Ticks, TimeSpan.Zero);

    internal static string CreateAddressKey(
        string tenantId,
        string addressValue,
        DateTimeOffset windowStart
    ) =>
        ExpiringKeys.Create(
            Purpose,
            tenantId,
            AddressPartition,
            addressValue,
            windowStart.UtcTicks.ToString(CultureInfo.InvariantCulture)
        );

    internal static string CreateAddressClientKey(
        string tenantId,
        string addressValue,
        string clientId,
        DateTimeOffset windowStart
    ) =>
        ExpiringKeys.Create(
            Purpose,
            tenantId,
            AddressClientPartition,
            addressValue,
            clientId,
            windowStart.UtcTicks.ToString(CultureInfo.InvariantCulture)
        );

    /// <summary>
    /// What a check and a count of one request share: the tenant, the partitioned remote address, the tenant's limits,
    /// and the window the request falls in.
    /// </summary>
    private readonly record struct AttemptScope(
        string TenantId,
        string AddressValue,
        int AddressLimit,
        int ClientLimit,
        TimeSpan Window,
        DateTimeOffset WindowStart,
        DateTimeOffset Now
    );
}
