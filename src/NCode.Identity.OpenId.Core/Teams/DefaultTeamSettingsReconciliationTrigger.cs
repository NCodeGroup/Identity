// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Threading.Channels;

namespace NCode.Identity.OpenId.Teams;

/// <summary>
/// Provides a default implementation of the <see cref="ITeamSettingsReconciliationTrigger"/> abstraction that writes
/// each request to the <see cref="Channel{T}"/> the <see cref="TeamSettingsReconciliationService"/> reads. The channel
/// is bounded and drops its oldest request when it is full, which loses nothing: the service reconciles every team
/// when it starts and whenever the platform's settings change, and a request for one team is repeated by its next
/// change.
/// </summary>
// See ADR-1219 and ADR-1411.
internal sealed class DefaultTeamSettingsReconciliationTrigger(Channel<string?> channel)
    : ITeamSettingsReconciliationTrigger
{
    /// <summary>
    /// Contains the number of requests that wait for the service before the oldest is dropped.
    /// </summary>
    internal const int Capacity = 64;

    private Channel<string?> Requests { get; } = channel;

    /// <summary>
    /// Creates the channel that carries the requests from the trigger to the service.
    /// </summary>
    /// <returns>The new <see cref="Channel{T}"/>.</returns>
    internal static Channel<string?> CreateChannel() =>
        Channel.CreateBounded<string?>(
            new BoundedChannelOptions(Capacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            }
        );

    /// <inheritdoc />
    public void Trigger(string? teamId) => Requests.Writer.TryWrite(teamId);
}
