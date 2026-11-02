// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using StackExchange.Redis;

namespace NCode.Identity.SharedState.Valkey.Facades;

/// <summary>
/// Provides the database of the one connection a process holds to the Valkey server, so the code that issues commands
/// can be tested without a server.
/// </summary>
internal interface IValkeyConnection
{
    /// <summary>
    /// Gets the database to issue commands against, connecting on first use.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="IDatabase"/> to issue commands against.</returns>
    ValueTask<IDatabase> GetDatabaseAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Gets the subscriber of the one connection, connecting on first use.
    /// </summary>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ISubscriber"/> to publish and subscribe with.</returns>
    ValueTask<ISubscriber> GetSubscriberAsync(CancellationToken cancellationToken);
}
