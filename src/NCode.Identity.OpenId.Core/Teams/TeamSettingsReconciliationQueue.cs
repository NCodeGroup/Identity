// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Channels;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides the queue that carries reconciliation requests from the <see cref="DefaultTeamSettingsReconciliationTrigger"/>
/// to the <see cref="TeamSettingsReconciliationService"/>. Requests coalesce and none is dropped: the queue holds one
/// entry per requested team, and a request for every team covers each pending team. Its memory is bounded by the number
/// of distinct teams, never by the rate of requests.
/// </summary>
// See ADR-1412.
internal sealed class TeamSettingsReconciliationQueue
{
    private Lock Gate { get; } = new();

    private HashSet<string> PendingTeamIds { get; } = new(StringComparer.OrdinalIgnoreCase);

    private bool EveryTeamPending { get; set; }

    // The one-slot signal only wakes the reader, which drains everything pending at once.
    private Channel<bool> Signal { get; } =
        Channel.CreateBounded<bool>(
            new BoundedChannelOptions(1)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
            }
        );

    /// <summary>
    /// Adds a reconciliation request to the queue.
    /// </summary>
    /// <param name="teamId">
    /// The identifier of the team to reconcile, or <see langword="null"/> to reconcile every team.
    /// </param>
    public void Add(string? teamId)
    {
        lock (Gate)
        {
            if (teamId is null)
            {
                EveryTeamPending = true;
                PendingTeamIds.Clear();
            }
            else if (!EveryTeamPending)
            {
                PendingTeamIds.Add(teamId);
            }
        }

        Signal.Writer.TryWrite(true);
    }

    /// <summary>
    /// Takes every pending request, waiting until one is added when none is pending.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> to observe while waiting.</param>
    /// <returns>The <see cref="TeamSettingsReconciliationRequest"/> that covers every pending request.</returns>
    public async ValueTask<TeamSettingsReconciliationRequest> TakeAsync(
        CancellationToken cancellationToken
    )
    {
        while (true)
        {
            if (TryTake(out var request))
                return request;

            await Signal.Reader.ReadAsync(cancellationToken);
        }
    }

    private bool TryTake(out TeamSettingsReconciliationRequest request)
    {
        lock (Gate)
        {
            if (!EveryTeamPending && PendingTeamIds.Count == 0)
            {
                request = default;
                return false;
            }

            request = new TeamSettingsReconciliationRequest(EveryTeamPending, [.. PendingTeamIds]);
            EveryTeamPending = false;
            PendingTeamIds.Clear();
            return true;
        }
    }
}
