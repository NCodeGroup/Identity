// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Events.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.Events</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>14000</c>–<c>14999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 14000;

    public const int EventHandlerFailed = Base + 1;
    public const int EventPublished = Base + 2;
    public const int AuditRecorded = Base + 5;
    public const int BackgroundEventDropped = Base + 6;
}
