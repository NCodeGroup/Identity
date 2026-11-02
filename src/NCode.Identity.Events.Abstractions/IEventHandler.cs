// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using JetBrains.Annotations;

namespace NCode.Identity.Events;

/// <summary>
/// Defines a subscriber that observes a published <typeparamref name="TEvent"/>.
/// </summary>
/// <remarks>
/// Handlers are registered as a strategy collection and invoked with fault isolation: a handler that
/// throws is logged and the failure is contained, so it never aborts the publish or the operation that
/// raised the event. The type parameter is contravariant, so a handler registered for a base event type
/// (for example <see cref="IEvent"/> or an audit base) observes every derived event via hierarchical
/// dispatch. Implement <see cref="ISupportHandlerPriority"/> to control invocation order.
/// </remarks>
/// <typeparam name="TEvent">The type of event this handler observes.</typeparam>
[PublicAPI]
public interface IEventHandler<in TEvent>
    where TEvent : IEvent
{
    /// <summary>
    /// Asynchronously observes the published <paramref name="event"/>.
    /// </summary>
    /// <param name="event">The event that was published.</param>
    /// <param name="cancellationToken">
    /// The <see cref="CancellationToken"/> that may be used to cancel the asynchronous operation.
    /// </param>
    /// <returns>
    /// A <see cref="ValueTask"/> that represents the asynchronous operation.
    /// </returns>
    ValueTask HandleAsync(TEvent @event, CancellationToken cancellationToken);
}
