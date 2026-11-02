// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.Dispatch.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.Dispatch</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.JobDropped,
        Level = LogLevel.Warning,
        Message = "The in-process dispatch queue is full; job '{JobType}' was dropped. Use a durable dispatcher adapter for at-least-once delivery."
    )]
    internal static partial void JobDropped(this ILogger logger, string jobType);

    [LoggerMessage(
        EventId = EventIds.JobHandlerFailed,
        Level = LogLevel.Warning,
        Message = "Dispatch job '{JobType}' failed on attempt {Attempt} of {MaxAttempts}; a retry is scheduled."
    )]
    internal static partial void JobHandlerFailed(
        this ILogger logger,
        string jobType,
        int attempt,
        int maxAttempts,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.JobAttemptsExhausted,
        Level = LogLevel.Error,
        Message = "Dispatch job '{JobType}' failed on its final attempt {Attempt} of {MaxAttempts} and was abandoned."
    )]
    internal static partial void JobAttemptsExhausted(
        this ILogger logger,
        string jobType,
        int attempt,
        int maxAttempts,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.NoJobHandlerRegistered,
        Level = LogLevel.Error,
        Message = "No '{HandlerType}' is registered to execute dispatch job '{JobType}'; the job was discarded."
    )]
    internal static partial void NoJobHandlerRegistered(
        this ILogger logger,
        string handlerType,
        string jobType
    );
}
