// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using NCode.Identity.OpenId.Environments;
using NCode.Identity.OpenId.Logging;
using NCode.Identity.OpenId.Persistence.DataContracts;
using NCode.Identity.OpenId.Persistence.Stores;
using NCode.Identity.OpenId.Platform;
using NCode.Identity.Persistence.Stores;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Reconciles everything derived from each team's effective settings in the background, whenever those settings differ
/// from the ones they were last reconciled against. The service materializes the platform when the replica starts, and
/// when that fails it waits until a request materializes it. It reconciles every team once, again whenever the
/// platform's settings provider signals a change (a configuration reload, a read of the committed settings document,
/// or a poll that saw a change), and one team and every team beneath it whenever an
/// <see cref="ITeamSettingsReconciliationTrigger"/> names it, each team once per pass.
/// The recorded fingerprint makes a team that changed nothing cost one read. A reconciliation that is unsettled,
/// because another replica holds its lease or the pass failed, is attempted again when the
/// <see cref="OpenIdTeamOptions.ReconciliationLeaseDuration"/> has passed. Requests that arrive during a pass coalesce
/// and none is lost. The service never throws on stop.
/// </summary>
// See ADR-1411, ADR-1412, ADR-1426.
internal sealed class TeamSettingsReconciliationService(
    IMaterializedPlatformRegistry materializedPlatformRegistry,
    IOpenIdPlatformProvider platformProvider,
    IOpenIdEnvironmentProvider environmentProvider,
    IStoreManagerFactory storeManagerFactory,
    ITeamSettingsReconciler teamSettingsReconciler,
    ITeamDescendantWalker teamDescendantWalker,
    TeamSettingsReconciliationQueue queue,
    IOptions<OpenIdOptions> optionsAccessor,
    TimeProvider timeProvider,
    ILogger<TeamSettingsReconciliationService> logger
) : BackgroundService
{
    // The teams are read in pages, so the pass never holds every team at once.
    private const int TeamPageSize = 100;

    // Names the pass over every team in the log when a failure is not about one team.
    private const string EveryTeam = "*";

    private IMaterializedPlatformRegistry MaterializedPlatformRegistry { get; } =
        materializedPlatformRegistry;
    private IOpenIdPlatformProvider PlatformProvider { get; } = platformProvider;
    private IOpenIdEnvironmentProvider EnvironmentProvider { get; } = environmentProvider;
    private IStoreManagerFactory StoreManagerFactory { get; } = storeManagerFactory;
    private ITeamSettingsReconciler TeamSettingsReconciler { get; } = teamSettingsReconciler;
    private ITeamDescendantWalker TeamDescendantWalker { get; } = teamDescendantWalker;
    private TeamSettingsReconciliationQueue Queue { get; } = queue;
    private IOptions<OpenIdOptions> OptionsAccessor { get; } = optionsAccessor;
    private TimeProvider TimeProvider { get; } = timeProvider;
    private ILogger<TeamSettingsReconciliationService> Logger { get; } = logger;

    // Read on use, never in the constructor, so the options are not built while the container composes this service.
    private OpenIdOptions Options => OptionsAccessor.Value;

    // The teams whose last pass was unsettled, and whether the pass over every team did not finish.
    private HashSet<string> Unsettled { get; } = new(StringComparer.Ordinal);
    private bool EveryTeamUnsettled { get; set; }

    // When the unsettled teams are attempted again: one lease duration after the pass that left them unsettled. A
    // trigger never moves it, and it is null while nothing is unsettled.
    private DateTimeOffset? RetryDeadline { get; set; }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var platform = await GetPlatformAsync(stoppingToken);

            // The provider signals a configuration reload, a refresh of the committed document, and a poll that saw one;
            // each can change every team's effective settings.
            using var registration = ChangeToken.OnChange(
                platform.SettingsProvider.GetChangeToken,
                () => Queue.Add(null)
            );
            Queue.Add(null);

            while (!stoppingToken.IsCancellationRequested)
            {
                var (triggered, request) = await WaitForTriggerAsync(stoppingToken);
                if (triggered)
                {
                    await ReconcileTriggeredAsync(request, stoppingToken);
                    UpdateRetryDeadline();
                }

                // A trigger never moves the deadline, so a steady stream of triggers does not starve a retry.
                if (!triggered || IsRetryDue())
                    await RetryAsync(stoppingToken);
            }
        }
        catch (Exception) when (stoppingToken.IsCancellationRequested)
        {
            // Expected on shutdown; a hosted service never throws on stop.
        }
    }

    private async ValueTask<OpenIdPlatform> GetPlatformAsync(CancellationToken stoppingToken)
    {
        try
        {
            var environment = EnvironmentProvider.Get();
            return await PlatformProvider.GetAsync(environment, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // The database may be unreachable while the replica starts; the first request materializes the platform.
            Logger.PlatformMaterializationAtStartFailed(exception);
            return await MaterializedPlatformRegistry.GetPlatformAsync(stoppingToken);
        }
    }

    private async ValueTask<(
        bool Triggered,
        TeamSettingsReconciliationRequest Request
    )> WaitForTriggerAsync(CancellationToken stoppingToken)
    {
        if (Unsettled.Count == 0 && !EveryTeamUnsettled)
            return (true, await Queue.TakeAsync(stoppingToken));

        // An unsettled reconciliation is attempted again when the lease another replica may hold has expired, which is
        // the retry deadline; the wait for a trigger ends there.
        var now = TimeProvider.GetUtcNow();
        var remaining = (RetryDeadline ?? now) - now;
        if (remaining < TimeSpan.Zero)
            remaining = TimeSpan.Zero;

        using var leaseExpired = new CancellationTokenSource(remaining, TimeProvider);
        using var wait = CancellationTokenSource.CreateLinkedTokenSource(
            stoppingToken,
            leaseExpired.Token
        );
        try
        {
            return (true, await Queue.TakeAsync(wait.Token));
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // The lease expired.
            return (false, default);
        }
    }

    private async ValueTask ReconcileTriggeredAsync(
        TeamSettingsReconciliationRequest request,
        CancellationToken stoppingToken
    )
    {
        if (request.EveryTeam)
            await ReconcileEveryTeamAsync(stoppingToken);
        else
            await ReconcileSubtreesAsync(request.TeamIds, stoppingToken);
    }

    private async ValueTask RetryAsync(CancellationToken stoppingToken)
    {
        // The deadline is cleared first, so a retry that leaves a team unsettled sets a fresh one.
        RetryDeadline = null;
        if (EveryTeamUnsettled)
            await ReconcileEveryTeamAsync(stoppingToken);
        else
            await ReconcileSubtreesAsync(Unsettled.ToList(), stoppingToken);

        UpdateRetryDeadline();
    }

    private bool IsRetryDue() =>
        RetryDeadline is { } deadline && TimeProvider.GetUtcNow() >= deadline;

    // Sets the deadline when a pass leaves something unsettled and none is set, and clears it when nothing is.
    private void UpdateRetryDeadline()
    {
        if (Unsettled.Count == 0 && !EveryTeamUnsettled)
            RetryDeadline = null;
        else
            RetryDeadline ??= TimeProvider.GetUtcNow() + Options.Team.ReconciliationLeaseDuration;
    }

    private async ValueTask ReconcileEveryTeamAsync(CancellationToken stoppingToken)
    {
        try
        {
            var page = new PageRequest { Limit = TeamPageSize };
            do
            {
                // Each page reads its team identifiers in its own unit of work.
                var result = await GetTeamPageAsync(page, stoppingToken);
                foreach (var team in result.Items)
                    await ReconcileTeamAsync(team.TeamId, stoppingToken);

                page = page with { Cursor = result.NextCursor };
            } while (page.Cursor is not null);

            EveryTeamUnsettled = false;
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            Logger.TeamSettingsReconciliationFailed(EveryTeam, exception);
            EveryTeamUnsettled = true;
        }
    }

    private async ValueTask<PagedResult<PersistedTeam>> GetTeamPageAsync(
        PageRequest page,
        CancellationToken stoppingToken
    )
    {
        await using var storeManager = await StoreManagerFactory.CreateAsync(stoppingToken);
        var store = storeManager.GetStore<ITeamStore>();
        return await store.GetPageAsync(page, stoppingToken);
    }

    // Reconciles each team and every team beneath it, each team once per pass. Only the requested teams are remembered,
    // so a pass holds no set of every team it reaches.
    private async ValueTask ReconcileSubtreesAsync(
        IReadOnlyCollection<string> teamIds,
        CancellationToken stoppingToken
    )
    {
        var requested = new HashSet<string>(teamIds, StringComparer.OrdinalIgnoreCase);
        var reconciled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var teamId in teamIds)
        {
            // The team's subtree was reached through an ancestor in this pass.
            if (!reconciled.Add(teamId))
                continue;

            await ReconcileTeamAsync(teamId, stoppingToken);

            try
            {
                await TeamDescendantWalker.WalkDescendantsAsync(
                    teamId,
                    (team, token) => VisitDescendantAsync(team, requested, reconciled, token),
                    stoppingToken
                );
            }
            catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
            {
                Logger.TeamSettingsReconciliationFailed(teamId, exception);
                Unsettled.Add(teamId);
            }
        }
    }

    // A requested team that was already reconciled was walked from itself, so its subtree is declined here.
    private async ValueTask<bool> VisitDescendantAsync(
        PersistedTeam team,
        HashSet<string> requested,
        HashSet<string> reconciled,
        CancellationToken stoppingToken
    )
    {
        if (requested.Contains(team.TeamId) && !reconciled.Add(team.TeamId))
            return false;

        await ReconcileTeamAsync(team.TeamId, stoppingToken);
        return true;
    }

    private async ValueTask ReconcileTeamAsync(string teamId, CancellationToken stoppingToken)
    {
        var settled = false;
        try
        {
            settled = await TeamSettingsReconciler.ReconcileAsync(teamId, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            Logger.TeamSettingsReconciliationFailed(teamId, exception);
        }

        if (settled)
            Unsettled.Remove(teamId);
        else
            Unsettled.Add(teamId);
    }
}
