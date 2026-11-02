// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
