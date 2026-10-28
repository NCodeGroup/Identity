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
/// A universal subscriber that emits one structured log entry for every published
/// <see cref="IAuditEvent"/>. The entry's state is the flattened audit record (<c>audit.*</c> keys), so
/// a log sink indexes the same fields the record declares, and its level follows the event's
/// <see cref="IAuditEvent.Severity"/>: <see cref="AuditSeverities.Critical"/> at critical,
/// <see cref="AuditSeverities.Warning"/> at warning and any other severity at information. The entry is
/// written to the <see cref="CategoryName"/> category, so an operator can route the audit trail on its own.
/// </summary>
internal class DefaultAuditLoggingHandler(ILoggerFactory loggerFactory)
    : IEventHandler<IAuditEvent>,
        ISupportHandlerPriority
{
    /// <summary>
    /// The logger category of every audit log entry.
    /// </summary>
    internal const string CategoryName = "NCode.Identity.Audit";

    /// <summary>
    /// The original-format template of every audit log entry, whose placeholders name the
    /// <c>audit.*</c> keys of the entry's state.
    /// </summary>
    internal const string MessageTemplate = "Audit '{audit.event_name}' {audit.outcome}.";

    private ILogger Logger { get; } = loggerFactory.CreateLogger(CategoryName);

    /// <inheritdoc />
    public int HandlerPriority => DefaultHandlerPriorities.High;

    /// <inheritdoc />
    public ValueTask HandleAsync(IAuditEvent @event, CancellationToken cancellationToken)
    {
        var level = GetLogLevel(@event.Severity);
        if (!Logger.IsEnabled(level))
        {
            return ValueTask.CompletedTask;
        }

        var state = AuditRecordAttributes.Create(@event);
        state.Add(new("{OriginalFormat}", MessageTemplate));

        var message = $"Audit '{@event.EventName}' {@event.Outcome}.";

        Logger.Log(
            level,
            new EventId(EventIds.AuditRecorded, nameof(EventIds.AuditRecorded)),
            state,
            null,
            (_, _) => message
        );

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Maps an audit severity code to the log level of its entry.
    /// </summary>
    /// <param name="severity">The severity code of the audit event.</param>
    /// <returns>The log level of the audit log entry.</returns>
    internal static LogLevel GetLogLevel(string severity) =>
        severity switch
        {
            AuditSeverities.Critical => LogLevel.Critical,
            AuditSeverities.Warning => LogLevel.Warning,
            _ => LogLevel.Information,
        };
}
