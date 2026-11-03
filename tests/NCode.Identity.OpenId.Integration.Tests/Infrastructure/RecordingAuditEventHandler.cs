// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Concurrent;
using NCode.Identity.Events;
using NCode.Identity.Events.Audit;

namespace NCode.Identity.OpenId.Integration.Tests.Infrastructure;

/// <summary>
/// An <see cref="IEventHandler{TEvent}"/> that records every audit event the host publishes, so a test can assert what
/// an operation audited. The publisher delivers it inside the request, so the events of a request are recorded once its
/// response arrives.
/// </summary>
public sealed class RecordingAuditEventHandler : IEventHandler<IAuditEvent>
{
    /// <summary>
    /// Gets the recorded audit events, in the order they were published.
    /// </summary>
    public ConcurrentQueue<IAuditEvent> Events { get; } = new();

    /// <inheritdoc />
    public ValueTask HandleAsync(IAuditEvent @event, CancellationToken cancellationToken)
    {
        Events.Enqueue(@event);
        return ValueTask.CompletedTask;
    }
}
