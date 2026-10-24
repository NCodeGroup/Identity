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
/// The adapter port for a set of counters that each expire at an absolute instant and that every replica of a
/// deployment observes. It is the primitive behind fixed-window rate limiting: a caller names one counter per partition
/// and window (for example a tenant, a client, and the window's start), increments it once per request, and compares
/// the count with the partition's limit. Its central operation is an <b>atomic</b> increment, so concurrent requests on
/// every replica that shares the set are each counted exactly once. The default adapter is in-process and therefore
/// correct only for a single replica; a deployment that runs several replicas registers a shared adapter (for example
/// Valkey) so a request counted by one replica is seen by all of them. A caller that counts a run of events, such as
/// consecutive failures, removes the counter when the run ends. A caller that judges several limits at once names
/// their counters in one ordered batch, which a networked adapter runs in one round trip.
/// </summary>
[PublicAPI]
public interface IExpiringCounterSet
{
    /// <summary>
    /// Atomically increments the counter named <paramref name="key"/> and returns its count after the increment. A
    /// counter that is absent or expired starts at one and expires at <paramref name="expiresWhen"/>; the expiry of a
    /// counter that is present is left unchanged, so the counter spans one fixed window. Of any number of concurrent
    /// calls for the same key, across every replica that shares the set, each observes a distinct count.
    /// </summary>
    /// <param name="key">The name of the counter. Keys are compared ordinally.</param>
    /// <param name="expiresWhen">
    /// The instant after which a counter this call starts is no longer present. A counter whose expiry is not in the
    /// future cannot be recorded, so nothing is counted and the method returns zero.
    /// </param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>
    /// The count after the increment; or zero if nothing was counted because <paramref name="expiresWhen"/> is not in
    /// the future.
    /// </returns>
    ValueTask<long> IncrementAsync(
        string key,
        DateTimeOffset expiresWhen,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Increments the counters of an ordered batch, one after the other, and stops after the first counter whose count
    /// exceeds its limit: the count after its increment plus the count of its reference counter, if it has one, times
    /// its reference weight. Each increment is atomic, as <see cref="IncrementAsync"/> is. A networked adapter runs the
    /// batch in one round trip. The keys of one batch share a hash tag, the text between the first <c>{</c> and the next
    /// <c>}</c> of a key, so a clustered store keeps them on one node.
    /// </summary>
    /// <param name="increments">The counters to increment, in the order they are incremented.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>
    /// One <see cref="ExpiringCounterCount"/> per counter that was reached, in order. Fewer counts than increments, or a
    /// last count over its limit, means the batch stopped there.
    /// </returns>
    ValueTask<IReadOnlyList<ExpiringCounterCount>> IncrementUntilExceededAsync(
        IReadOnlyList<ExpiringCounterIncrement> increments,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets the count of the counter named <paramref name="key"/> without incrementing it.
    /// </summary>
    /// <param name="key">The name of the counter. Keys are compared ordinally.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The count of the counter; or zero if it is absent or expired.</returns>
    ValueTask<long> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the counter named <paramref name="key"/>, so the next increment starts it again at one with a new
    /// expiry. Removing a counter that is absent or expired does nothing.
    /// </summary>
    /// <param name="key">The name of the counter. Keys are compared ordinally.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>A <see cref="ValueTask"/> that completes when the counter is no longer present.</returns>
    ValueTask RemoveAsync(string key, CancellationToken cancellationToken);
}
