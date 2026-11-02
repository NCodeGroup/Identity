// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

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
}
