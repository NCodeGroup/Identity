// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.OpenId.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.OpenId.Core</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.UnhandledException,
        Level = LogLevel.Error,
        Message = "An unexpected error occurred while processing the request {DisplayName}"
    )]
    internal static partial void UnhandledException(
        this ILogger logger,
        Exception exception,
        string displayName
    );

    [LoggerMessage(
        EventId = EventIds.ClientError,
        Level = LogLevel.Warning,
        Message = "The request {DisplayName} was rejected with a client error: {ErrorCode}"
    )]
    internal static partial void ClientError(
        this ILogger logger,
        Exception exception,
        string displayName,
        string errorCode
    );

    [LoggerMessage(
        EventId = EventIds.PlatformSettingsReloadRejected,
        Level = LogLevel.Error,
        Message = "The reloaded platform settings in '{SectionPath}' cannot be honored, so the previous settings remain in force: {Errors}"
    )]
    internal static partial void PlatformSettingsReloadRejected(
        this ILogger logger,
        string sectionPath,
        string errors
    );

    [LoggerMessage(
        EventId = EventIds.TenantKeyRolloverFailed,
        Level = LogLevel.Warning,
        Message = "Rolling over the keys of the tenant '{TenantId}' failed; they are rolled over again at the next pass or when the tenant is next materialized"
    )]
    internal static partial void TenantKeyRolloverFailed(
        this ILogger logger,
        string tenantId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TenantKeyRolloverTeamFailed,
        Level = LogLevel.Warning,
        Message = "Rolling over the keys of the tenants of the team '{TeamId}' failed; they are rolled over again at the next pass"
    )]
    internal static partial void TenantKeyRolloverTeamFailed(
        this ILogger logger,
        string teamId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TenantKeyRolloverPassFailed,
        Level = LogLevel.Warning,
        Message = "The tenant key rollover pass failed; it runs again at the next interval"
    )]
    internal static partial void TenantKeyRolloverPassFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TenantSeedPostCommitStepFailed,
        Level = LogLevel.Warning,
        Message = "A step that follows the committed seed of the tenant '{TenantId}' failed; the seed stands, and the remaining steps run"
    )]
    internal static partial void TenantSeedPostCommitStepFailed(
        this ILogger logger,
        string tenantId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TenantKeyAuditPublishFailed,
        Level = LogLevel.Warning,
        Message = "Publishing the audit events of the committed key changes of the tenant '{TenantId}' failed; the changes stand"
    )]
    internal static partial void TenantKeyAuditPublishFailed(
        this ILogger logger,
        string tenantId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TenantKeyRolloverLostRace,
        Level = LogLevel.Debug,
        Message = "Another writer changed the keys of the tenant '{TenantId}' first; the rollover pass leaves them to it"
    )]
    internal static partial void TenantKeyRolloverLostRace(this ILogger logger, string tenantId);

    [LoggerMessage(
        EventId = EventIds.TenantKeyReseedFailed,
        Level = LogLevel.Warning,
        Message = "Re-seeding the tenant '{TenantId}' after a platform settings change failed; it is seeded again when it is next materialized"
    )]
    internal static partial void TenantKeyReseedFailed(
        this ILogger logger,
        string tenantId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TenantChangeBroadcastFailed,
        Level = LogLevel.Warning,
        Message = "Announcing the change to tenant '{TenantId}' to the other replicas failed; they pick it up through their polls"
    )]
    internal static partial void TenantChangeBroadcastFailed(
        this ILogger logger,
        string tenantId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.PlatformSettingsRefreshFailed,
        Level = LogLevel.Warning,
        Message = "Refreshing the platform's persisted settings failed; the previous settings remain in force until the next refresh"
    )]
    internal static partial void PlatformSettingsRefreshFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.PlatformSettingsChangeRefreshFailed,
        Level = LogLevel.Warning,
        Message = "Reading the settings of the platform again after a change failed; this replica applies the change at its next poll"
    )]
    internal static partial void PlatformSettingsChangeRefreshFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.PlatformSettingsChangeBroadcastFailed,
        Level = LogLevel.Warning,
        Message = "Announcing the change to the settings of the platform to the other replicas failed; they apply it at their next poll"
    )]
    internal static partial void PlatformSettingsChangeBroadcastFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TeamSettingsReconciliationStarted,
        Level = LogLevel.Information,
        Message = "Reconciling the tenants and clients that the settings '{Fingerprint}' of the team '{TeamId}' affect"
    )]
    internal static partial void TeamSettingsReconciliationStarted(
        this ILogger logger,
        string teamId,
        string fingerprint
    );

    [LoggerMessage(
        EventId = EventIds.TeamSettingsReconciled,
        Level = LogLevel.Information,
        Message = "Reconciled the tenants and clients that the settings '{Fingerprint}' of the team '{TeamId}' affect"
    )]
    internal static partial void TeamSettingsReconciled(
        this ILogger logger,
        string teamId,
        string fingerprint
    );

    [LoggerMessage(
        EventId = EventIds.TeamSettingsReconciliationFailed,
        Level = LogLevel.Warning,
        Message = "Reconciling the settings of the team '{TeamId}' failed; it is attempted again when the reconciliation lease expires"
    )]
    internal static partial void TeamSettingsReconciliationFailed(
        this ILogger logger,
        string teamId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.PlatformMaterializationAtStartFailed,
        Level = LogLevel.Warning,
        Message = "Materializing the platform when the replica started failed; its settings are reconciled once a request materializes it"
    )]
    internal static partial void PlatformMaterializationAtStartFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ReconciledTeamSettingsUnreadable,
        Level = LogLevel.Warning,
        Message = "The settings that the team '{TeamId}' was last reconciled against cannot be read, so every tenant and client is reconciled"
    )]
    internal static partial void ReconciledTeamSettingsUnreadable(
        this ILogger logger,
        string teamId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TenantCacheClearFailed,
        Level = LogLevel.Warning,
        Message = "Evicting the cached tenants after the platform's settings changed failed; each tenant applies the change when its cache entry expires"
    )]
    internal static partial void TenantCacheClearFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.TeamChangeBroadcastFailed,
        Level = LogLevel.Warning,
        Message = "Announcing the change to team '{TeamId}' to the other replicas failed; they pick it up through their polls"
    )]
    internal static partial void TeamChangeBroadcastFailed(
        this ILogger logger,
        string teamId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TeamCacheClearFailed,
        Level = LogLevel.Warning,
        Message = "Evicting the cached teams failed; each team applies the change when its cache entry expires"
    )]
    internal static partial void TeamCacheClearFailed(this ILogger logger, Exception exception);

    [LoggerMessage(
        EventId = EventIds.ProtectedValuePageConflict,
        Level = LogLevel.Debug,
        Message = "Another writer changed a page of the protected values of '{Owner}' while it was rewrapped; reading the page again (attempt {Attempt})"
    )]
    internal static partial void ProtectedValuePageConflict(
        this ILogger logger,
        SecretOwner owner,
        int attempt
    );

    [LoggerMessage(
        EventId = EventIds.ProtectedValueRewrapFailed,
        Level = LogLevel.Warning,
        Message = "The protected value of record '{RecordId}' of '{Owner}' cannot be rewrapped; it keeps its master key"
    )]
    internal static partial void ProtectedValueRewrapFailed(
        this ILogger logger,
        Exception exception,
        SecretOwner owner,
        string recordId
    );

    [LoggerMessage(
        EventId = EventIds.ProtectedValuePageAbandoned,
        Level = LogLevel.Warning,
        Message = "A page of the protected values of '{Owner}' changed on each of {Attempts} attempts; its values are left for the next pass"
    )]
    internal static partial void ProtectedValuePageAbandoned(
        this ILogger logger,
        Exception exception,
        SecretOwner owner,
        int attempts
    );

    [LoggerMessage(
        EventId = EventIds.ProtectedValuePageUnreadable,
        Level = LogLevel.Warning,
        Message = "A master key that a page of the protected values of '{Owner}' names cannot be loaded; its values are left for the next pass"
    )]
    internal static partial void ProtectedValuePageUnreadable(
        this ILogger logger,
        Exception exception,
        SecretOwner owner
    );

    [LoggerMessage(
        EventId = EventIds.MasterKeyMaintenancePassFailed,
        Level = LogLevel.Warning,
        Message = "The master key maintenance pass failed; it runs again at the next interval"
    )]
    internal static partial void MasterKeyMaintenancePassFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.MasterKeyMaintenanceOwnerFailed,
        Level = LogLevel.Warning,
        Message = "The master key maintenance of '{Owner}' failed; the pass continues with the other owners"
    )]
    internal static partial void MasterKeyMaintenanceOwnerFailed(
        this ILogger logger,
        SecretOwner owner,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.MasterKeyRotationLostRace,
        Level = LogLevel.Debug,
        Message = "Another writer rotated the master key of '{Owner}' first; the maintenance pass leaves it to that rotation"
    )]
    internal static partial void MasterKeyRotationLostRace(this ILogger logger, SecretOwner owner);

    [LoggerMessage(
        EventId = EventIds.MasterKeyAuditPublishFailed,
        Level = LogLevel.Warning,
        Message = "Publishing the audit event of the committed master key change of '{Owner}' failed; the change stands"
    )]
    internal static partial void MasterKeyAuditPublishFailed(
        this ILogger logger,
        SecretOwner owner,
        Exception exception
    );
}
