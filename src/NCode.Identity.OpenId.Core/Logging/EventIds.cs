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

    public const int ServerSettingsReloadRejected = Base + 3;

    public const int TenantKeyReseedFailed = Base + 5;

    public const int TenantChangeBroadcastFailed = Base + 6;

    public const int ServerSettingsRefreshFailed = Base + 7;

    public const int ServerSettingsChangeRefreshFailed = Base + 8;

    public const int ServerSettingsChangeBroadcastFailed = Base + 9;

    public const int ServerSettingsReconciliationStarted = Base + 10;

    public const int ServerSettingsReconciled = Base + 11;

    public const int ServerSettingsReconciliationFailed = Base + 12;

    public const int ReconciledServerSettingsUnreadable = Base + 13;

    public const int TenantCacheClearFailed = Base + 14;

    public const int ServerMaterializationAtStartFailed = Base + 15;

    public const int TenantKeyRolloverFailed = Base + 19;

    public const int TenantKeyRolloverPassFailed = Base + 20;

    public const int TenantKeyRolloverLostRace = Base + 21;

    public const int TenantSeedPostCommitStepFailed = Base + 23;

    public const int TenantKeyAuditPublishFailed = Base + 24;

    public const int TenantKeyExtended = Base + 22;
}
