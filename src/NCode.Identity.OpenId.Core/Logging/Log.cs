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
        EventId = EventIds.TenantKeyProvisioned,
        Level = LogLevel.Information,
        Message = "Provisioned the tenant '{TenantId}' with a new {Algorithm} key '{SecretId}' that signs from {ActivatesWhen} to {RetiresWhen}"
    )]
    internal static partial void TenantKeyProvisioned(
        this ILogger logger,
        string tenantId,
        string algorithm,
        string secretId,
        DateTimeOffset activatesWhen,
        DateTimeOffset retiresWhen
    );

    [LoggerMessage(
        EventId = EventIds.TenantKeyActivated,
        Level = LogLevel.Information,
        Message = "Activated the published {Algorithm} key '{SecretId}' of the tenant '{TenantId}' at once, because no key of its algorithm signed"
    )]
    internal static partial void TenantKeyActivated(
        this ILogger logger,
        string tenantId,
        string? algorithm,
        string secretId
    );

    [LoggerMessage(
        EventId = EventIds.TenantKeyRetired,
        Level = LogLevel.Information,
        Message = "Retired the {Algorithm} key '{SecretId}' of the tenant '{TenantId}', which its settings no longer use; it expires at {ExpiresWhen}"
    )]
    internal static partial void TenantKeyRetired(
        this ILogger logger,
        string tenantId,
        string? algorithm,
        string secretId,
        DateTimeOffset expiresWhen
    );

    [LoggerMessage(
        EventId = EventIds.TenantKeyExtended,
        Level = LogLevel.Information,
        Message = "Extended the {Algorithm} key '{SecretId}' of the tenant '{TenantId}' to expire at {ExpiresWhen}, the end of the retirement grace as it stands"
    )]
    internal static partial void TenantKeyExtended(
        this ILogger logger,
        string tenantId,
        string? algorithm,
        string secretId,
        DateTimeOffset expiresWhen
    );

    [LoggerMessage(
        EventId = EventIds.TenantKeyRemoved,
        Level = LogLevel.Information,
        Message = "Removed the expired {Algorithm} key '{SecretId}' of the tenant '{TenantId}'"
    )]
    internal static partial void TenantKeyRemoved(
        this ILogger logger,
        string tenantId,
        string? algorithm,
        string secretId
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
}
