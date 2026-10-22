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
/// The adapter port that announces a message to every replica of a deployment. It is the primitive behind cache
/// coherence — a tenant changed on one replica must be evicted from the cache of every other replica — so a replica
/// publishes a message on a channel and each replica's <see cref="IReplicaBroadcastHandler"/> for that channel receives
/// it. The default adapter is in-process and therefore correct only for a single replica; a deployment that runs several
/// replicas registers a shared adapter (for example Valkey) so a message published by one replica reaches all of them.
/// </summary>
// See ADR-0205.
[PublicAPI]
public interface IReplicaBroadcaster
{
    /// <summary>
    /// Delivers <paramref name="message"/> to every replica's <see cref="IReplicaBroadcastHandler"/> for
    /// <paramref name="channel"/>, this replica's included. Delivery is best effort, at most once, and unordered, so
    /// handlers must be idempotent and tolerate a lost message. A handler's failure never reaches the publisher.
    /// </summary>
    /// <param name="channel">The channel to publish on. Channels are compared ordinally.</param>
    /// <param name="message">The message to deliver.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask PublishAsync(string channel, string message, CancellationToken cancellationToken);
}
