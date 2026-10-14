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
