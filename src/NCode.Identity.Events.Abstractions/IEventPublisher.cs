// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Events;

/// <summary>
/// Publishes events to their registered <see cref="IEventHandler{TEvent}"/> subscribers.
/// </summary>
/// <remarks>
/// Publishing an event notifies handlers registered for the event's runtime type and for any event
/// type it derives from or implements (hierarchical dispatch), so a handler registered for a base
/// event type observes every derived event. Handlers are invoked with fault isolation: a handler that
/// throws is logged and the failure is contained, so it never aborts the publish or the caller.
/// </remarks>
[PublicAPI]
public interface IEventPublisher
{
    /// <summary>
    /// Asynchronously publishes <paramref name="event"/> to all matching handlers, ordered by priority
    /// and invoked with fault isolation.
    /// </summary>
    /// <param name="event">The event to publish.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <typeparam name="TEvent">The compile-time type of the event; dispatch uses its runtime type.</typeparam>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken)
        where TEvent : IEvent;

    /// <summary>
    /// Determines whether any handler is registered for <typeparamref name="TEvent"/> or a base event
    /// type it derives from, allowing a caller to skip assembling an expensive payload when nothing is
    /// listening.
    /// </summary>
    /// <typeparam name="TEvent">The type of event to probe for subscribers.</typeparam>
    /// <returns>
    /// <see langword="true"/> if at least one matching handler is registered; otherwise,
    /// <see langword="false"/>.
    /// </returns>
    bool HasSubscribers<TEvent>()
        where TEvent : IEvent;
}
