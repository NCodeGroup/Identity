// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.Dispatch.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.Dispatch</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>15000</c>–<c>15999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 15000;

    public const int JobDropped = Base + 1;
    public const int JobHandlerFailed = Base + 2;
    public const int JobAttemptsExhausted = Base + 3;
    public const int NoJobHandlerRegistered = Base + 4;
}
