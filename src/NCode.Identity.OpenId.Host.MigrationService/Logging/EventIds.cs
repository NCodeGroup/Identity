// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Host.MigrationService.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.OpenId.Host.MigrationService</c> worker.
/// </summary>
/// <remarks>
/// This package reserves the <c>20000</c>–<c>20999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 20000;

    public const int MigrationsApplying = Base + 1;

    public const int MigrationsApplied = Base + 2;

    public const int MigrationsFailed = Base + 3;
}
