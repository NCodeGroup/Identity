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

    public const int TenantKeyRolloverFailed = Base + 19;

    public const int TenantKeyRolloverPassFailed = Base + 20;

    public const int TenantKeyRolloverLostRace = Base + 21;

    public const int TenantSeedPostCommitStepFailed = Base + 23;

    public const int TenantKeyAuditPublishFailed = Base + 24;

    public const int TenantKeyExtended = Base + 22;

    public const int ProtectedValuePageConflict = Base + 25;

    public const int MasterKeyMaintenancePassFailed = Base + 26;

    public const int MasterKeyMaintenanceOwnerFailed = Base + 27;

    public const int MasterKeyRotationLostRace = Base + 28;

    public const int MasterKeyAuditPublishFailed = Base + 29;

    public const int ProtectedValueRewrapFailed = Base + 30;

    public const int ProtectedValuePageAbandoned = Base + 31;

    public const int ProtectedValuePageUnreadable = Base + 32;

    public const int TenantKeyRolloverTeamFailed = Base + 33;

    public const int OwnerRootKeyValidationPassFailed = Base + 34;

    public const int OwnerRootKeyValidationOwnerFailed = Base + 35;

    public const int OwnerRootKeyAuditPublishFailed = Base + 36;
}
