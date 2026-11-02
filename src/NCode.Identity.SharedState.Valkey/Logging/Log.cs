// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.SharedState.Valkey.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.SharedState.Valkey</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.ReplicaBroadcastSubscribeFailed,
        Level = LogLevel.Warning,
        Message = "Subscribing to the replica broadcast channels failed; retrying"
    )]
    internal static partial void ReplicaBroadcastSubscribeFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ReplicaBroadcastHandlerFailed,
        Level = LogLevel.Error,
        Message = "A handler of the replica broadcast channel '{Channel}' failed"
    )]
    internal static partial void ReplicaBroadcastHandlerFailed(
        this ILogger logger,
        string channel,
        Exception exception
    );
}
