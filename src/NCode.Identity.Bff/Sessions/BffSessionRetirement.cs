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
    /// Ends the stored sessions of the frontends that the set removed or configured as another client.
    /// </summary>
    /// <param name="frontends">The set of frontends that the BFF serves.</param>
    /// <param name="cancellationToken">The token to cancel the pass.</param>
    /// <returns>
    /// <see langword="true"/> when every stale session was ended or already gone and no stale frontend was left to
    /// another holder of its lease; otherwise, <see langword="false"/>.
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
            var stale = new List<(BffSessionClient Client, string Reason)>();
            foreach (var client in group)
            {
                var reason = GetEndReason(client, frontends);
                if (reason is not null)
                    stale.Add((client, reason));
            }

            if (stale.Count == 0)
                continue;

            var lease = await store.TryAcquireRefreshLeaseAsync(
                "retire:" + frontend,
                timeProvider.GetUtcNow() + frontends.Options.SessionRetirement.LeaseDuration,
                cancellationToken
            );
            if (lease is null)
            {
                complete = false;
                continue;
            }

            try
            {
                foreach (var (client, reason) in stale)
                {
                    if (!await RetireClientAsync(client, reason, frontends, cancellationToken))
                        complete = false;
                }
            }
            finally
            {
                await store.ReleaseRefreshLeaseAsync(lease.Value, CancellationToken.None);
            }
        }

        return complete;
    }

    private async ValueTask<bool> RetireClientAsync(
        BffSessionClient client,
        string reason,
        BffFrontendSet frontends,
        CancellationToken cancellationToken
    )
    {
        var complete = true;
        var count = 0;
        var keys = await store.FindByClientAsync(client, cancellationToken);
        foreach (var key in keys)
        {
            var record = await store.GetAsync(key, cancellationToken);
            if (record is null)
                continue;

            var metadata = record.Metadata;
            var recordReason = GetEndReason(metadata.Client, frontends);
            if (recordReason is null)
                continue;

            try
            {
                if (await sessions.EndAsync(null, key, metadata, recordReason, cancellationToken))
                    ++count;
            }
            catch (Exception exception)
                when (exception is not BffStoreUnavailableException
                    && !cancellationToken.IsCancellationRequested
                )
            {
                logger.FrontendSessionRetirementFailed(exception, client.Frontend, recordReason);
                complete = false;
            }
        }

        if (count > 0)
            logger.FrontendSessionsEnded(count, client.Frontend, reason);

        return complete;
    }

    private static string? GetEndReason(BffSessionClient client, BffFrontendSet frontends)
    {
        if (!frontends.TryGet(client.Frontend, out var found))
            return BffSessionEndReasons.FrontendRemoved;

        if (!found.IsConfiguredAs(client))
            return BffSessionEndReasons.ClientChanged;

        return null;
    }
}
