// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.SharedState.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.SharedState</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>16000</c>–<c>16999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 16000;

    public const int ReplicaBroadcastHandlerFailed = Base + 1;
}
