// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

namespace NCode.Identity.OpenId.Logging;

/// <summary>
/// Defines the reserved logging event IDs for the <c>NCode.Identity.OpenId.Core</c> package.
/// </summary>
/// <remarks>
/// This package reserves the <c>1000</c>–<c>1999</c> band per the logging conventions. Values are append-only:
/// never renumber or reuse a shipped identifier.
/// </remarks>
internal static class EventIds
{
    private const int Base = 1000;

    public const int UnhandledException = Base + 1;

    public const int ClientError = Base + 2;

    public const int PlatformSettingsReloadRejected = Base + 3;

    public const int TenantKeyProvisioned = Base + 4;

    public const int TenantKeyReseedFailed = Base + 5;

    public const int TenantChangeBroadcastFailed = Base + 6;

    public const int PlatformSettingsRefreshFailed = Base + 7;

    public const int PlatformSettingsChangeRefreshFailed = Base + 8;

    public const int PlatformSettingsChangeBroadcastFailed = Base + 9;

    public const int TeamSettingsReconciliationStarted = Base + 10;

    public const int TeamSettingsReconciled = Base + 11;

    public const int TeamSettingsReconciliationFailed = Base + 12;

    public const int ReconciledTeamSettingsUnreadable = Base + 13;

    public const int TenantCacheClearFailed = Base + 14;

    public const int PlatformMaterializationAtStartFailed = Base + 15;

    public const int TeamChangeBroadcastFailed = Base + 16;

    public const int TeamCacheClearFailed = Base + 17;
}
