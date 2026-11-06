// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Watches the BFF session that a long-lived connection holds until the session ends.
/// </summary>
[PublicAPI]
public interface IBffConnectionRevalidator
{
    /// <summary>
    /// Revalidates the session that <paramref name="connection"/> holds every
    /// <see cref="IBffConnectionSession.RevalidationInterval"/> without sliding it, logs an unavailable session
    /// store and tries again at the next interval, and completes once the session ended, at the next revalidation or
    /// as soon as <see cref="IBffConnectionSession.SessionEnded"/> is cancelled, or revalidating it failed for any
    /// other reason.
    /// </summary>
    /// <param name="connection">The session that the connection holds.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <exception cref="OperationCanceledException">The <paramref name="cancellationToken"/> was cancelled.</exception>
    ValueTask WaitUntilEndedAsync(
        IBffConnectionSession connection,
        CancellationToken cancellationToken
    );
}
