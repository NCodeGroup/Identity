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

namespace NCode.Identity.Server.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.Server</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.BootstrapAdminClientSeeded,
        Level = LogLevel.Information,
        Message = "Seeded bootstrap administrator client '{ClientId}' into tenant '{TenantId}'"
    )]
    internal static partial void BootstrapAdminClientSeeded(
        this ILogger logger,
        string clientId,
        string tenantId
    );

    [LoggerMessage(
        EventId = EventIds.BootstrapAdminClientAlreadyExists,
        Level = LogLevel.Debug,
        Message = "Bootstrap administrator client already exists in tenant '{TenantId}'; skipping seeding"
    )]
    internal static partial void BootstrapAdminClientAlreadyExists(
        this ILogger logger,
        string tenantId
    );
}
