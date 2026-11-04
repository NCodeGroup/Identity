// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Servers;

/// <summary>
/// Brings everything derived from a server's effective settings in line with them whenever those settings differ from
/// the ones they were last reconciled against.
/// </summary>
// See ADR-1219.
internal interface IServerSettingsReconciler
{
    /// <summary>
    /// Reconciles the server's effective settings when they differ from the ones recorded as reconciled, with one
    /// replica per change.
    /// </summary>
    /// <param name="serverId">The identifier of the server whose effective settings are reconciled.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> used to cancel the operation.</param>
    /// <returns>
    /// A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation. The result is
    /// <see langword="true"/> when the server's effective settings are reconciled, whether already or by this call, and
    /// <see langword="false"/> when the caller should try again later because another replica holds the lease or the
    /// record lost a race with a change to the server.
    /// </returns>
    ValueTask<bool> ReconcileAsync(string serverId, CancellationToken cancellationToken);
}
