// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;

namespace NCode.Identity.OpenId.Host.MigrationService.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.OpenId.Host.MigrationService</c>
/// worker.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.MigrationsApplying,
        Level = LogLevel.Information,
        Message = "Applying host database migrations."
    )]
    internal static partial void MigrationsApplying(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.MigrationsApplied,
        Level = LogLevel.Information,
        Message = "Host database migrations applied successfully."
    )]
    internal static partial void MigrationsApplied(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.MigrationsFailed,
        Level = LogLevel.Error,
        Message = "Host database migration failed."
    )]
    internal static partial void MigrationsFailed(this ILogger logger, Exception exception);
}
