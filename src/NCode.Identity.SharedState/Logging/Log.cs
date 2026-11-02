// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.SharedState.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.SharedState</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
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
