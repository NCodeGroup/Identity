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
