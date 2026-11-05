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
        EventId = EventIds.ServerSettingsReloadRejected,
        Level = LogLevel.Error,
        Message = "The reloaded server settings in '{SectionPath}' cannot be honored, so the previous settings remain in force: {Errors}"
    )]
    internal static partial void ServerSettingsReloadRejected(
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
        Message = "Re-seeding the tenant '{TenantId}' after a server settings change failed; it is seeded again when it is next materialized"
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
        EventId = EventIds.ServerSettingsRefreshFailed,
        Level = LogLevel.Warning,
        Message = "Refreshing the server's persisted settings failed; the previous settings remain in force until the next refresh"
    )]
    internal static partial void ServerSettingsRefreshFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ServerSettingsChangeRefreshFailed,
        Level = LogLevel.Warning,
        Message = "Reading the settings of the server '{ServerId}' again after a change failed; this replica applies the change at its next poll"
    )]
    internal static partial void ServerSettingsChangeRefreshFailed(
        this ILogger logger,
        string serverId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ServerSettingsChangeBroadcastFailed,
        Level = LogLevel.Warning,
        Message = "Announcing the change to the settings of the server '{ServerId}' to the other replicas failed; they apply it at their next poll"
    )]
    internal static partial void ServerSettingsChangeBroadcastFailed(
        this ILogger logger,
        string serverId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ServerSettingsReconciliationStarted,
        Level = LogLevel.Information,
        Message = "Reconciling the tenants and clients that the settings '{Fingerprint}' of the server '{ServerId}' affect"
    )]
    internal static partial void ServerSettingsReconciliationStarted(
        this ILogger logger,
        string serverId,
        string fingerprint
    );

    [LoggerMessage(
        EventId = EventIds.ServerSettingsReconciled,
        Level = LogLevel.Information,
        Message = "Reconciled the tenants and clients that the settings '{Fingerprint}' of the server '{ServerId}' affect"
    )]
    internal static partial void ServerSettingsReconciled(
        this ILogger logger,
        string serverId,
        string fingerprint
    );

    [LoggerMessage(
        EventId = EventIds.ServerSettingsReconciliationFailed,
        Level = LogLevel.Warning,
        Message = "Reconciling the settings of the server '{ServerId}' failed; it is attempted again when the reconciliation lease expires"
    )]
    internal static partial void ServerSettingsReconciliationFailed(
        this ILogger logger,
        string serverId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ServerMaterializationAtStartFailed,
        Level = LogLevel.Warning,
        Message = "Materializing the server when the replica started failed; its settings are reconciled once a request materializes it"
    )]
    internal static partial void ServerMaterializationAtStartFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ReconciledServerSettingsUnreadable,
        Level = LogLevel.Warning,
        Message = "The settings that the server '{ServerId}' was last reconciled against cannot be read, so every tenant and client is reconciled"
    )]
    internal static partial void ReconciledServerSettingsUnreadable(
        this ILogger logger,
        string serverId,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TenantCacheClearFailed,
        Level = LogLevel.Warning,
        Message = "Evicting the cached tenants after the server's settings changed failed; each tenant applies the change when its cache entry expires"
    )]
    internal static partial void TenantCacheClearFailed(this ILogger logger, Exception exception);

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

    [LoggerMessage(
        EventId = EventIds.OwnerRootKeyValidationPassFailed,
        Level = LogLevel.Warning,
        Message = "The owner root key validation pass failed; it runs again at the next interval"
    )]
    internal static partial void OwnerRootKeyValidationPassFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.OwnerRootKeyValidationOwnerFailed,
        Level = LogLevel.Warning,
        Message = "Validating the root key of '{Owner}' failed; the pass continues with the other owners"
    )]
    internal static partial void OwnerRootKeyValidationOwnerFailed(
        this ILogger logger,
        SecretOwner owner,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.OwnerRootKeyAuditPublishFailed,
        Level = LogLevel.Warning,
        Message = "Publishing the audit event of the committed root key change of '{Owner}' failed; the change stands"
    )]
    internal static partial void OwnerRootKeyAuditPublishFailed(
        this ILogger logger,
        SecretOwner owner,
        Exception exception
    );
}
