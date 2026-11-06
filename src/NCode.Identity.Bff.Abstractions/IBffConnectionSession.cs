// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Security.Claims;
using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// Represents a BFF session that a long-lived connection holds.
/// </summary>
[PublicAPI]
public interface IBffConnectionSession : IBffTokenContext
{
    /// <summary>
    /// Gets how often the holder of the connection revalidates the session, which is the frontend's
    /// <c>Session:RevalidationInterval</c>.
    /// </summary>
    TimeSpan RevalidationInterval { get; }

    /// <summary>
    /// Gets a token that is cancelled once a use of the connection finds that its session ended: a revalidation, a
    /// token request, or a refresh that ends the session. From then on <see cref="ValidateAsync"/> returns
    /// <see langword="null"/> without reloading the session.
    /// </summary>
    CancellationToken SessionEnded { get; }

    /// <summary>
    /// Reloads the session, ends it when it expired or a validator refuses it, and slides its idle window from
    /// <paramref name="lastActivityAt"/> when that is later than its last access.
    /// </summary>
    /// <param name="lastActivityAt">The time of the connection's latest activity, or <see langword="null"/> for none.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    /// <returns>The principal of the session, or <see langword="null"/> once the session ended.</returns>
    ValueTask<ClaimsPrincipal?> ValidateAsync(
        DateTimeOffset? lastActivityAt,
        CancellationToken cancellationToken
    );
}
