// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;
using NCode.Identity.Events.Logging;

namespace NCode.Identity.Events;

/// <summary>
/// A universal subscriber that emits a low-level trace breadcrumb naming the type of every published
/// event. A generic handler only sees an opaque <see cref="IEvent"/>, so this records that an event
/// fired, not its contents; actionable, structured logging comes from handlers of a known contract
/// such as <see cref="DefaultAuditLoggingHandler"/>.
/// </summary>
internal class DefaultEventLoggingHandler(ILogger<DefaultEventLoggingHandler> logger)
    : IEventHandler<IEvent>,
        ISupportHandlerPriority
{
    private ILogger<DefaultEventLoggingHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public int HandlerPriority => DefaultHandlerPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(IEvent @event, CancellationToken cancellationToken)
    {
        Logger.EventPublished(@event.GetType().FullName ?? @event.GetType().Name);
        return ValueTask.CompletedTask;
    }
}
