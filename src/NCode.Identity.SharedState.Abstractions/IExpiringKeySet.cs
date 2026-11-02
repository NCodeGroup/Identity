// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.SharedState;

/// <summary>
/// The adapter port for a set of keys that each expire at an absolute instant and that every replica of a deployment
/// observes. It is the primitive behind single-use and freshness checks — a JSON Web Token (JWT) identifier that must
/// not be accepted twice, a server-issued nonce that must be recognized by whichever replica receives it — so its
/// central operation is an <b>atomic</b> add-if-absent. The default adapter is in-process and therefore correct only
/// for a single replica; a deployment that runs several replicas registers a shared adapter (for example Valkey) so a
/// key recorded by one replica is seen by all of them.
/// </summary>
[PublicAPI]
public interface IExpiringKeySet
{
    /// <summary>
    /// Atomically adds <paramref name="key"/> to the set unless it is already present and unexpired. Of any number of
    /// concurrent calls for the same key, across every replica that shares the set, at most one returns
    /// <see langword="true"/> until the key expires.
    /// </summary>
    /// <param name="key">The key to add. Keys are compared ordinally.</param>
    /// <param name="expiresWhen">
    /// The instant after which the key is no longer present and may be added again. A key whose expiry is not in the
    /// future cannot be recorded, so it is not added and the method returns <see langword="false"/>.
    /// </param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> if the key was added; <see langword="false"/> if it was already present and unexpired, or
    /// its expiry is not in the future.
    /// </returns>
    ValueTask<bool> TryAddAsync(
        string key,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Determines whether <paramref name="key"/> is present in the set and unexpired.
    /// </summary>
    /// <param name="key">The key to look up. Keys are compared ordinally.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns><see langword="true"/> if the key is present and unexpired; otherwise, <see langword="false"/>.</returns>
    ValueTask<bool> ContainsAsync(string key, CancellationToken cancellationToken);
}
