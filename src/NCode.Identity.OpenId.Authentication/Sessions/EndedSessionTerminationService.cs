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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NCode.Identity.OpenId.Authentication.Logging;
using NCode.Identity.OpenId.Contexts;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Servers;
using NCode.Identity.SharedState;
using NCode.Persistence.Stores;

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// Terminates the sessions that ended while no request used them, every
/// <see cref="EndedSessionTerminationOptions.Interval"/>, once the replica has materialized its server. One replica
/// runs each interval's pass: it reads the sessions that are not revoked and have ended in pages of
/// <see cref="PageSize"/> sessions, and terminates each in the context of its tenant, which revokes the grants issued
/// under it, notifies its relying parties, and audits the termination. A pass that fails is logged and run again at the
/// next interval.
/// </summary>
// See ADR-0223.
internal sealed class EndedSessionTerminationService(
    IMaterializedServerAccessor materializedServerAccessor,
    IServiceScopeFactory serviceScopeFactory,
    IStoreManagerFactory storeManagerFactory,
    IOpenIdContextFactory openIdContextFactory,
    ISessionService sessionService,
    IExpiringKeySet expiringKeySet,
    IOptions<EndedSessionTerminationOptions> optionsAccessor,
    TimeProvider timeProvider,
    ILogger<EndedSessionTerminationService> logger
) : BackgroundService
{
    /// <summary>
    /// Contains the purpose that prefixes the key of the lease that one replica takes for each interval.
    /// </summary>
    internal const string LeasePurpose = "ended-session-termination";

    /// <summary>
    /// The number of sessions read in one page.
    /// </summary>
    internal const int PageSize = 100;

    private IMaterializedServerAccessor MaterializedServerAccessor { get; } =
        materializedServerAccessor;
    private IServiceScopeFactory ServiceScopeFactory { get; } = serviceScopeFactory;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private IOpenIdContextFactory OpenIdContextFactory { get; } = openIdContextFactory;
    private ISessionService SessionService { get; } = sessionService;
    private IExpiringKeySet ExpiringKeySet { get; } = expiringKeySet;
    private IOptions<EndedSessionTerminationOptions> OptionsAccessor { get; } = optionsAccessor;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<EndedSessionTerminationService> Logger { get; } = logger;

    // Read on use, never in the constructor, so the options are not built while the container composes this service.
    private EndedSessionTerminationOptions Options => OptionsAccessor.Value;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // The pass waits for the replica's first request, so it never reads the store while the host starts.
            await MaterializedServerAccessor.GetServerAsync(stoppingToken);

            using var timer = new PeriodicTimer(Options.Interval, TimeProvider);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await TerminateEndedSessionsAsync(stoppingToken);
            }
        }
        catch (Exception) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown; a hosted service never throws on stop.
        }
    }

    /// <summary>
    /// Terminates the sessions that ended while no request used them, unless another replica holds the lease of this
    /// interval. A pass that fails is logged and does not throw.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    internal async ValueTask TerminateEndedSessionsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var now = TimeProvider.GetUtcNow();

            // Replicas that run in one interval share the key, so one of them runs the pass. A pass that two replicas
            // run anyway is harmless because each session is terminated once.
            var interval = Options.Interval;
            var window = now.UtcTicks / interval.Ticks;
            var leaseKey = $"{LeasePurpose}:{window}";
            var leaseExpiresWhen = new DateTimeOffset((window + 1) * interval.Ticks, TimeSpan.Zero);
            if (!await ExpiringKeySet.TryAddAsync(leaseKey, leaseExpiresWhen, cancellationToken))
                return;

            var count = 0;
            string? cursor = null;
            do
            {
                var page = await GetPageAsync(now, cursor, cancellationToken);

                var sessionIdsByTenant = new Dictionary<string, List<string>>(
                    StringComparer.Ordinal
                );
                foreach (var grant in page.Items)
                {
                    if (grant.SessionId is not { Length: > 0 } sessionId)
                        continue;

                    if (!sessionIdsByTenant.TryGetValue(grant.TenantId, out var sessionIds))
                    {
                        sessionIds = [];
                        sessionIdsByTenant.Add(grant.TenantId, sessionIds);
                    }

                    sessionIds.Add(sessionId);
                }

                foreach (var (tenantId, sessionIds) in sessionIdsByTenant)
                {
                    count += await TerminateTenantSessionsAsync(
                        tenantId,
                        sessionIds,
                        cancellationToken
                    );
                }

                cursor = page.NextCursor;
            } while (cursor is not null);

            if (count > 0)
                Logger.EndedSessionsTerminated(count);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.EndedSessionTerminationFailed(exception);
        }
    }

    /// <summary>
    /// Reads one page of the sessions that are not revoked and have ended at <paramref name="now"/> in its own unit of
    /// work.
    /// </summary>
    /// <param name="now">The time at or before which the end of a session selects it.</param>
    /// <param name="cursor">The cursor of the page to read; <see langword="null"/> reads the first page.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the page of session grants.</returns>
    private async ValueTask<PagedResult<PersistedGrant>> GetPageAsync(
        DateTimeOffset now,
        string? cursor,
        CancellationToken cancellationToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(cancellationToken);
        var store = storeManager.GetStore<IGrantStore>();
        return await store.GetUnrevokedExpiredPageAsync(
            OpenIdConstants.PersistedGrantTypes.Session,
            now,
            new PageRequest { Cursor = cursor, Limit = PageSize },
            cancellationToken
        );
    }

    /// <summary>
    /// Terminates the specified sessions of one tenant that have ended, in a scope and a context of their own. A tenant
    /// whose sessions cannot be terminated is logged and skipped, so the other tenants' sessions are terminated.
    /// </summary>
    /// <param name="tenantId">The identifier of the tenant of the sessions.</param>
    /// <param name="sessionIds">The opaque identifiers (<c>sid</c>) of the sessions.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation, containing the count of sessions
    /// that this call terminated.</returns>
    private async ValueTask<int> TerminateTenantSessionsAsync(
        string tenantId,
        List<string> sessionIds,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await using var scope = ServiceScopeFactory.CreateAsyncScope();
            await using var openIdContext = await OpenIdContextFactory.CreateForBackgroundAsync(
                scope.ServiceProvider,
                tenantId,
                cancellationToken
            );

            var count = 0;
            foreach (var sessionId in sessionIds)
            {
                if (
                    await SessionService.TerminateIfEndedAsync(
                        openIdContext,
                        sessionId,
                        cancellationToken
                    )
                )
                {
                    ++count;
                }
            }

            return count;
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            Logger.EndedSessionTerminationFailedForTenant(tenantId, exception);
            return 0;
        }
    }
}
