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

namespace NCode.Identity.Bff.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.Bff</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.LoginFailed,
        Level = LogLevel.Debug,
        Message = "The BFF login failed: {Reason}"
    )]
    internal static partial void LoginFailed(this ILogger logger, string reason);

    [LoggerMessage(
        EventId = EventIds.RefreshFailed,
        Level = LogLevel.Warning,
        Message = "The BFF token refresh failed: {Reason}"
    )]
    internal static partial void RefreshFailed(
        this ILogger logger,
        string reason,
        Exception? exception
    );
}
