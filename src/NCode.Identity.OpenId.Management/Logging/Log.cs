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
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.OpenId.Management</c> package:
/// the expected client-input failures that map to a <c>4xx</c> response, whose exception detail reaches operators
/// without being exposed to the caller, the stored settings documents a settings write cannot read and does not
/// check, and the stored documents a management request cannot honor.
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

    [LoggerMessage(
        EventId = EventIds.LostConcurrencyRace,
        Level = LogLevel.Debug,
        Message = "A management request lost a concurrency race with another request and was answered 412"
    )]
    internal static partial void LostConcurrencyRace(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.ClientSettingsUnreadable,
        Level = LogLevel.Warning,
        Message = "The settings document of client '{ClientId}' of tenant '{TenantId}' cannot be honored, so a settings change was not checked against it: {DocumentError}"
    )]
    internal static partial void ClientSettingsUnreadable(
        this ILogger logger,
        string tenantId,
        string clientId,
        string documentError
    );

    [LoggerMessage(
        EventId = EventIds.TenantSettingsUnreadable,
        Level = LogLevel.Warning,
        Message = "The settings document of tenant '{TenantId}' cannot be honored, so a settings change was not checked against the tenant's clients: {DocumentError}"
    )]
    internal static partial void TenantSettingsUnreadable(
        this ILogger logger,
        string tenantId,
        string documentError
    );

    [LoggerMessage(
        EventId = EventIds.ServerSettingsUnreadable,
        Level = LogLevel.Warning,
        Message = "The settings document of server '{ServerId}' that a settings change replaces cannot be honored, so the change was not checked against any client: {DocumentError}"
    )]
    internal static partial void ServerSettingsUnreadable(
        this ILogger logger,
        string serverId,
        string documentError
    );

    [LoggerMessage(
        EventId = EventIds.StoredSettingsUnreadable,
        Level = LogLevel.Warning,
        Message = "The stored settings document of {ScopeName} '{OwnerId}' cannot be honored, so a management request was answered 409: {DocumentError}"
    )]
    internal static partial void StoredSettingsUnreadable(
        this ILogger logger,
        string scopeName,
        string ownerId,
        string documentError
    );
}
