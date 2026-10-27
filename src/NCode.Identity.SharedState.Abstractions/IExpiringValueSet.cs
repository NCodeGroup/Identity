#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

using JetBrains.Annotations;

namespace NCode.Identity.SharedState;

/// <summary>
/// The adapter port for a set of versioned values that each expire at an absolute instant and that every replica of a
/// deployment observes. It is the primitive behind login transactions and sessions: a caller stores a value under a
/// key, reads it with its version, and replaces it only while the version it read is still current, so concurrent
/// updates on different replicas never overwrite one another. Its operations are <b>atomic</b> across every replica that
/// shares the set. The default adapter is in-process and therefore correct only for a single replica; a deployment that
/// runs several replicas registers a shared adapter (for example Valkey) so a value written by one replica is seen by
/// all of them.
/// </summary>
// See ADR-0267.
[PublicAPI]
public interface IExpiringValueSet
{
    /// <summary>
    /// Atomically adds the value named <paramref name="key"/> at version one when no unexpired value is present. Of any
    /// number of concurrent calls for the same key, across every replica that shares the set, at most one succeeds.
    /// </summary>
    /// <param name="key">The name of the value. Keys are compared ordinally.</param>
    /// <param name="value">The bytes to store.</param>
    /// <param name="expiresWhen">The instant after which the value is no longer present.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> if the value was added; or <see langword="false"/> if an unexpired value is already
    /// present or <paramref name="expiresWhen"/> is not in the future.
    /// </returns>
    ValueTask<bool> TryAddAsync(
        string key,
        ReadOnlyMemory<byte> value,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the value named <paramref name="key"/> with its version, without changing it.
    /// </summary>
    /// <param name="key">The name of the value. Keys are compared ordinally.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The value and its version; or <see langword="null"/> if it is absent or expired.</returns>
    ValueTask<ExpiringValue?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically replaces the value named <paramref name="key"/> when its version is
    /// <paramref name="expectedVersion"/>. On success the version becomes <paramref name="expectedVersion"/> plus one
    /// and the expiry becomes <paramref name="expiresWhen"/>. Of any number of concurrent calls that name the same
    /// version, across every replica that shares the set, at most one succeeds.
    /// </summary>
    /// <param name="key">The name of the value. Keys are compared ordinally.</param>
    /// <param name="expectedVersion">The version the caller read, which must still be current.</param>
    /// <param name="value">The bytes to store.</param>
    /// <param name="expiresWhen">The instant after which the replacement is no longer present.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>
    /// <see langword="true"/> if the value was replaced; or <see langword="false"/>, leaving the value unchanged, if it
    /// is absent or expired, its version differs from <paramref name="expectedVersion"/>, or
    /// <paramref name="expiresWhen"/> is not in the future.
    /// </returns>
    ValueTask<bool> TryReplaceAsync(
        string key,
        long expectedVersion,
        ReadOnlyMemory<byte> value,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Atomically reads and removes the value named <paramref name="key"/>. Of any number of concurrent calls for the
    /// same key, across every replica that shares the set, at most one returns the value.
    /// </summary>
    /// <param name="key">The name of the value. Keys are compared ordinally.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The value and its version that this call removed; or <see langword="null"/> if it is absent or expired.</returns>
    ValueTask<ExpiringValue?> TryTakeAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the value named <paramref name="key"/> regardless of its version. Removing a value that is absent or
    /// expired does nothing.
    /// </summary>
    /// <param name="key">The name of the value. Keys are compared ordinally.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the value is no longer present.</returns>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken);
}
