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

namespace NCode.Identity.Events;

/// <summary>
/// A bounded queue that defers an event for background delivery to the handlers that opt into it via
/// <see cref="ISupportBackgroundDelivery"/>.
/// </summary>
/// <remarks>
/// Enqueuing never blocks the caller. The queue is best-effort: when it is full an event is dropped
/// (and the drop is logged) rather than stalling the publishing path.
/// </remarks>
[PublicAPI]
public interface IBackgroundEventQueue
{
    /// <summary>
    /// Enqueues <paramref name="event"/> for background delivery, dropping it if the queue is full.
    /// </summary>
    /// <param name="event">The event to deliver on a background worker.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask EnqueueAsync(IEvent @event, CancellationToken cancellationToken);
}
