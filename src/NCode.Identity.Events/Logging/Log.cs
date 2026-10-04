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

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.Events.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.Events</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.EventHandlerFailed,
        Level = LogLevel.Error,
        Message = "The event handler '{HandlerType}' failed while handling event '{EventType}'; the failure is isolated and did not abort publishing."
    )]
    internal static partial void EventHandlerFailed(
        this ILogger logger,
        string handlerType,
        string eventType,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.EventPublished,
        Level = LogLevel.Trace,
        Message = "Event '{EventType}' was published."
    )]
    internal static partial void EventPublished(this ILogger logger, string eventType);

    [LoggerMessage(
        EventId = EventIds.BackgroundEventDropped,
        Level = LogLevel.Warning,
        Message = "The background event queue is full; event '{EventType}' was dropped."
    )]
    internal static partial void BackgroundEventDropped(this ILogger logger, string eventType);

    [LoggerMessage(
        EventId = EventIds.AuditSucceeded,
        Level = LogLevel.Information,
        Message = "Audit '{Action}' succeeded (outcome={Outcome}, subject={SubjectId}, client={ClientId}, tenant={TenantId}, correlation={CorrelationId}, eventId={EventId})."
    )]
    internal static partial void AuditSucceeded(
        this ILogger logger,
        string action,
        string outcome,
        string? subjectId,
        string? clientId,
        string? tenantId,
        string? correlationId,
        Guid eventId
    );

    [LoggerMessage(
        EventId = EventIds.AuditFailed,
        Level = LogLevel.Warning,
        Message = "Audit '{Action}' failed (outcome={Outcome}, subject={SubjectId}, client={ClientId}, tenant={TenantId}, correlation={CorrelationId}, eventId={EventId}, reason={Reason})."
    )]
    internal static partial void AuditFailed(
        this ILogger logger,
        string action,
        string outcome,
        string? subjectId,
        string? clientId,
        string? tenantId,
        string? correlationId,
        Guid eventId,
        string? reason
    );

    [LoggerMessage(
        EventId = EventIds.AuditRecorded,
        Level = LogLevel.Information,
        Message = "Audit '{Action}' recorded (outcome={Outcome}, subject={SubjectId}, client={ClientId}, tenant={TenantId}, correlation={CorrelationId}, eventId={EventId}, reason={Reason})."
    )]
    internal static partial void AuditRecorded(
        this ILogger logger,
        string action,
        string outcome,
        string? subjectId,
        string? clientId,
        string? tenantId,
        string? correlationId,
        Guid eventId,
        string? reason
    );
}
