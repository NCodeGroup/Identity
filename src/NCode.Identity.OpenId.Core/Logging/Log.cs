#region Copyright Preamble

// Copyright @ 2025 NCode Group
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

namespace NCode.Identity.OpenId.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.OpenId.Core</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.UnhandledException,
        Level = LogLevel.Error,
        Message = "An unexpected error occurred while processing the request {DisplayName}"
    )]
    internal static partial void UnhandledException(
        this ILogger logger,
        Exception exception,
        string displayName
    );

    [LoggerMessage(
        EventId = EventIds.ClientError,
        Level = LogLevel.Warning,
        Message = "The request {DisplayName} was rejected with a client error: {ErrorCode}"
    )]
    internal static partial void ClientError(
        this ILogger logger,
        Exception exception,
        string displayName,
        string errorCode
    );

    [LoggerMessage(
        EventId = EventIds.ServerSettingsReloadRejected,
        Level = LogLevel.Error,
        Message = "The reloaded server settings in '{SectionPath}' cannot be honored, so the previous settings remain in force: {Errors}"
    )]
    internal static partial void ServerSettingsReloadRejected(
        this ILogger logger,
        string sectionPath,
        string errors
    );

    [LoggerMessage(
        EventId = EventIds.TenantKeyProvisioned,
        Level = LogLevel.Information,
        Message = "Provisioned the tenant '{TenantId}' with a new {Algorithm} key '{SecretId}'"
    )]
    internal static partial void TenantKeyProvisioned(
        this ILogger logger,
        string tenantId,
        string algorithm,
        string secretId
    );

    [LoggerMessage(
        EventId = EventIds.TenantKeyReseedFailed,
        Level = LogLevel.Warning,
        Message = "Re-seeding the tenant '{TenantId}' after a server settings change failed; it is seeded again when it is next materialized"
    )]
    internal static partial void TenantKeyReseedFailed(
        this ILogger logger,
        string tenantId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TenantChangeBroadcastFailed,
        Level = LogLevel.Warning,
        Message = "Announcing the change to tenant '{TenantId}' to the other replicas failed; they pick it up through their polls"
    )]
    internal static partial void TenantChangeBroadcastFailed(
        this ILogger logger,
        string tenantId,
        Exception exception
    );
}
