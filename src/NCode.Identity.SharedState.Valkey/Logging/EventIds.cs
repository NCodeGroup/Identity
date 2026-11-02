// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.SharedState.Valkey.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.SharedState.Valkey</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>17000</c>–<c>17999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 17000;

    public const int ReplicaBroadcastSubscribeFailed = Base + 1;
    public const int ReplicaBroadcastHandlerFailed = Base + 2;
}
