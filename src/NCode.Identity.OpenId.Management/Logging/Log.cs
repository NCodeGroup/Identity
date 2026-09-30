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

namespace NCode.Identity.OpenId.Management.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.OpenId.Management</c> package.
/// These record the expected client-input failures that map to a <c>4xx</c> response so the original exception
/// detail is available to operators without being exposed to the caller.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.SecretGenerationFailed,
        Level = LogLevel.Debug,
        Message = "Secret generation failed for the supplied request parameters"
    )]
    internal static partial void SecretGenerationFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.JsonPatchFailed,
        Level = LogLevel.Debug,
        Message = "A JSON Patch document could not be applied"
    )]
    internal static partial void JsonPatchFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.ResourceConflict,
        Level = LogLevel.Debug,
        Message = "The operation was rejected because of a resource conflict"
    )]
    internal static partial void ResourceConflict(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.MissingDependency,
        Level = LogLevel.Debug,
        Message = "The operation referenced a resource that does not exist"
    )]
    internal static partial void MissingDependency(this ILogger logger, Exception exception);
}
