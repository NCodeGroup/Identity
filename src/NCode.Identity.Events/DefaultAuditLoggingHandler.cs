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

using Microsoft.Extensions.Logging;
using NCode.Identity.Events.Audit;
using NCode.Identity.Events.Logging;

namespace NCode.Identity.Events;

/// <summary>
/// A universal subscriber that emits a structured, actionable log record for every published
/// <see cref="IAuditEvent"/>, driven off its envelope. The reserved outcomes are logged specifically
/// (<see cref="AuditOutcome.Success"/> at information, <see cref="AuditOutcome.Failure"/> at warning);
/// any other outcome is recorded at information with its outcome captured structurally, so operators
/// can set their own severity policy for custom outcome codes.
/// </summary>
internal class DefaultAuditLoggingHandler(ILogger<DefaultAuditLoggingHandler> logger)
    : IEventHandler<IAuditEvent>,
        ISupportHandlerPriority
{
    private ILogger<DefaultAuditLoggingHandler> Logger { get; } = logger;

    /// <inheritdoc />
    public int HandlerPriority => DefaultHandlerPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(IAuditEvent @event, CancellationToken cancellationToken)
    {
        if (string.Equals(@event.Outcome, AuditOutcome.Success, StringComparison.Ordinal))
        {
            Logger.AuditSucceeded(
                @event.Action,
                @event.Outcome,
                @event.SubjectId,
                @event.ClientId,
                @event.TenantId,
                @event.CorrelationId,
                @event.EventId
            );
        }
        else if (string.Equals(@event.Outcome, AuditOutcome.Failure, StringComparison.Ordinal))
        {
            Logger.AuditFailed(
                @event.Action,
                @event.Outcome,
                @event.SubjectId,
                @event.ClientId,
                @event.TenantId,
                @event.CorrelationId,
                @event.EventId,
                @event.Reason
            );
        }
        else
        {
            Logger.AuditRecorded(
                @event.Action,
                @event.Outcome,
                @event.SubjectId,
                @event.ClientId,
                @event.TenantId,
                @event.CorrelationId,
                @event.EventId,
                @event.Reason
            );
        }

        return ValueTask.CompletedTask;
    }
}
