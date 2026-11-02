// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Events;

/// <summary>
/// Invokes a resolved <see cref="IEventHandler{TEvent}"/> for a published event whose concrete handler
/// type is known only at runtime, bridging the generic handler contract to a non-generic call.
/// </summary>
/// <param name="handler">The resolved handler instance.</param>
/// <param name="event">The event to hand to the handler.</param>
/// <param name="cancellationToken">The cancellation token.</param>
/// <returns>A <see cref="ValueTask"/> that represents the asynchronous operation.</returns>
internal delegate ValueTask EventHandlerInvoker(
    object handler,
    IEvent @event,
    CancellationToken cancellationToken
);
