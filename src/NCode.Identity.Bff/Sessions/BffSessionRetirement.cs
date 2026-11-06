// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.Bff.Frontends;
using NCode.Identity.Bff.Logging;

namespace NCode.Identity.Bff.Sessions;

/// <summary>
/// Ends every stored session whose frontend the set does not configure or configures as another client; one replica
/// works on a frontend at a time under a store lease.
/// </summary>
/// <remarks>
/// <para>
/// The pass reads the store, not a remembered set of frontends, so a restart or an interrupted pass converges on the
/// next run. The pass decides from each record's own metadata and ends the session without any request to the
/// provider.
/// </para>
/// </remarks>
// See ADR-1443.
internal class BffSessionRetirement(
    BffInstrumentedSessionStore store,
    IBffSessionManager sessions,
    TimeProvider timeProvider,
    ILogger<BffSessionRetirement> logger
)
{
    /// <summary>
    /// Gets how long a replica holds the lease of one frontend.
    /// </summary>
    internal static TimeSpan LeaseDuration { get; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Ends the stored sessions of the frontends that the set removed or configured as another client.
    /// </summary>
    /// <param name="frontends">The set of frontends that the BFF serves.</param>
    /// <param name="cancellationToken">The token to cancel the pass.</param>
    /// <returns>
    /// <see langword="true"/> when no stale frontend was left to another holder of its lease; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    /// <exception cref="BffStoreUnavailableException">The session store could not be read or written.</exception>
    public virtual async ValueTask<bool> RetireAsync(
        BffFrontendSet frontends,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(frontends);

        var complete = true;
        var clients = await store.FindClientsAsync(cancellationToken);
        var groups = clients.GroupBy(x => x.Frontend, StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var frontend = group.Key;
            var stale = group
                .Where(x =>
                    GetEndReason(x.Frontend, x.Authority, x.ClientId, frontends) is not null
                )
                .ToList();
            if (stale.Count == 0)
                continue;

            var lease = await store.TryAcquireRefreshLeaseAsync(
                "retire:" + frontend,
                timeProvider.GetUtcNow() + LeaseDuration,
                cancellationToken
            );
            if (lease is null)
            {
                complete = false;
                continue;
            }

            try
            {
                foreach (var client in stale)
                    await RetireClientAsync(client, frontends, cancellationToken);
            }
            finally
            {
                await store.ReleaseRefreshLeaseAsync(lease.Value, CancellationToken.None);
            }
        }

        return complete;
    }

    private async ValueTask RetireClientAsync(
        BffSessionClient client,
        BffFrontendSet frontends,
        CancellationToken cancellationToken
    )
    {
        var count = 0;
        var keys = await store.FindByClientAsync(client, cancellationToken);
        foreach (var key in keys)
        {
            var record = await store.GetAsync(key, cancellationToken);
            if (record is null)
                continue;

            var metadata = record.Metadata;
            var reason = GetEndReason(
                metadata.Frontend,
                metadata.Authority,
                metadata.ClientId,
                frontends
            );
            if (reason is null)
                continue;

            await sessions.EndAsync(null, key, metadata, reason, cancellationToken);
            ++count;
        }

        if (count == 0)
            return;

        var clientReason =
            GetEndReason(client.Frontend, client.Authority, client.ClientId, frontends)
            ?? BffSessionEndReasons.ClientChanged;
        logger.FrontendSessionsEnded(count, client.Frontend, clientReason);
    }

    private static string? GetEndReason(
        string frontend,
        string authority,
        string clientId,
        BffFrontendSet frontends
    )
    {
        if (!frontends.TryGet(frontend, out var found))
            return BffSessionEndReasons.FrontendRemoved;

        if (
            !string.Equals(found.Options.Authority, authority, StringComparison.Ordinal)
            || !string.Equals(found.Options.ClientId, clientId, StringComparison.Ordinal)
        )
            return BffSessionEndReasons.ClientChanged;

        return null;
    }
}
