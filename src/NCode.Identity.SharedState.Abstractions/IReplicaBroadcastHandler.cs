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
/// The strategy that receives the messages published on one channel of an <see cref="IReplicaBroadcaster"/>. A handler
/// is registered with <c>TryAddEnumerable</c>, and several handlers may share a channel. Channels are an open
/// vocabulary: a feature names its own channel and registers a handler for it, and the broadcaster needs no knowledge
/// of either. Delivery is best effort, at most once, and unordered, so a handler must be idempotent and tolerate a lost
/// message.
/// </summary>
// See ADR-0205.
[PublicAPI]
public interface IReplicaBroadcastHandler
{
    /// <summary>
    /// Gets the channel whose messages this handler receives. Channels are compared ordinally.
    /// </summary>
    string Channel { get; }

    /// <summary>
    /// Handles a message that was published on <see cref="Channel"/> by any replica, this replica included.
    /// </summary>
    /// <param name="message">The message that was published.</param>
    /// <param name="cancellationToken">The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.</param>
    /// <returns>The <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
    ValueTask HandleAsync(string message, CancellationToken cancellationToken);
}
