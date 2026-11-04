// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

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
        EventId = EventIds.TenantKeyProvisioned,
        Level = LogLevel.Information,
        Message = "Provisioned the tenant '{TenantId}' with a new {Algorithm} key '{SecretId}'"
    )]
    internal static partial void TenantKeyProvisioned(
        this ILogger logger,
        string tenantId,
        string algorithm,
        string secretId
    );

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
        Message = "Reading the settings of the platform  again after a change failed; this replica applies the change at its next poll"
    )]
    internal static partial void PlatformSettingsChangeRefreshFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.PlatformSettingsChangeBroadcastFailed,
        Level = LogLevel.Warning,
        Message = "Announcing the change to the settings of the platform  to the other replicas failed; they apply it at their next poll"
    )]
    internal static partial void PlatformSettingsChangeBroadcastFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.PlatformSettingsReconciliationStarted,
        Level = LogLevel.Information,
        Message = "Reconciling the tenants and clients that the settings '{Fingerprint}' of the platform '{PlatformId}' affect"
    )]
    internal static partial void PlatformSettingsReconciliationStarted(
        this ILogger logger,
        string platformId,
        string fingerprint
    );

    [LoggerMessage(
        EventId = EventIds.PlatformSettingsReconciled,
        Level = LogLevel.Information,
        Message = "Reconciled the tenants and clients that the settings '{Fingerprint}' of the platform '{PlatformId}' affect"
    )]
    internal static partial void PlatformSettingsReconciled(
        this ILogger logger,
        string platformId,
        string fingerprint
    );

    [LoggerMessage(
        EventId = EventIds.PlatformSettingsReconciliationFailed,
        Level = LogLevel.Warning,
        Message = "Reconciling the settings of the platform '{PlatformId}' failed; it is attempted again when the reconciliation lease expires"
    )]
    internal static partial void PlatformSettingsReconciliationFailed(
        this ILogger logger,
        string platformId,
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
        EventId = EventIds.ReconciledPlatformSettingsUnreadable,
        Level = LogLevel.Warning,
        Message = "The settings that the platform '{PlatformId}' was last reconciled against cannot be read, so every tenant and client is reconciled"
    )]
    internal static partial void ReconciledPlatformSettingsUnreadable(
        this ILogger logger,
        string platformId,
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
}
