// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;
using NCode.Identity.Secrets.Persistence.Protection;

namespace NCode.Identity.Server.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.Server</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.BootstrapAdminClientSeeded,
        Level = LogLevel.Information,
        Message = "Seeded bootstrap administrator client '{ClientId}' into tenant '{TenantId}'"
    )]
    internal static partial void BootstrapAdminClientSeeded(
        this ILogger logger,
        string clientId,
        string tenantId
    );

    [LoggerMessage(
        EventId = EventIds.BootstrapAdminClientSecretRefreshed,
        Level = LogLevel.Information,
        Message = "Refreshed the bootstrap administrator client '{ClientId}' secret in tenant '{TenantId}'"
    )]
    internal static partial void BootstrapAdminClientSecretRefreshed(
        this ILogger logger,
        string clientId,
        string tenantId
    );

    [LoggerMessage(
        EventId = EventIds.ConformanceClientSeeded,
        Level = LogLevel.Information,
        Message = "Seeded conformance client '{ClientId}' into tenant '{TenantId}'"
    )]
    internal static partial void ConformanceClientSeeded(
        this ILogger logger,
        string clientId,
        string tenantId
    );

    [LoggerMessage(
        EventId = EventIds.ConformanceTestUserSeeded,
        Level = LogLevel.Information,
        Message = "Seeded conformance test user '{UserName}' into tenant '{TenantId}'"
    )]
    internal static partial void ConformanceTestUserSeeded(
        this ILogger logger,
        string userName,
        string tenantId
    );

    [LoggerMessage(
        EventId = EventIds.LocalRootKeyInProduction,
        Level = LogLevel.Warning,
        Message = "The root key is a local file; prefer a key management service."
    )]
    internal static partial void LocalRootKeyInProduction(this ILogger logger);

    [LoggerMessage(
        EventId = EventIds.KeyRingKeyUndecryptable,
        Level = LogLevel.Error,
        Message = "The Data Protection key '{KeyId}' cannot be decrypted with the platform's master key."
    )]
    internal static partial void KeyRingKeyUndecryptable(
        this ILogger logger,
        Exception exception,
        Guid keyId
    );

    [LoggerMessage(
        EventId = EventIds.KeyRingKeyRevoked,
        Level = LogLevel.Warning,
        Message = "Revoked the Data Protection key '{KeyId}': {Reason}."
    )]
    internal static partial void KeyRingKeyRevoked(this ILogger logger, Guid keyId, string reason);

    [LoggerMessage(
        EventId = EventIds.KeyRingMasterKeyNotLoaded,
        Level = LogLevel.Error,
        Message = "The master key {MasterKeyId} that a Data Protection key names is missing or cannot be unwrapped; the key ring gate reports the keys it protects."
    )]
    internal static partial void KeyRingMasterKeyNotLoaded(
        this ILogger logger,
        Exception exception,
        Guid masterKeyId
    );

    [LoggerMessage(
        EventId = EventIds.PlatformSecretProtectorRefreshFailed,
        Level = LogLevel.Warning,
        Message = "Loading the platform's secret protector again failed; the protector in use stays, and the next refresh tries again."
    )]
    internal static partial void PlatformSecretProtectorRefreshFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.SecretProtectionCommandConfigurationInvalid,
        Level = LogLevel.Error,
        Message = "The secret-protection command cannot run: the configuration is invalid."
    )]
    internal static partial void SecretProtectionCommandConfigurationInvalid(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.SecretProtectionCommandFailed,
        Level = LogLevel.Error,
        Message = "The secret-protection command failed."
    )]
    internal static partial void SecretProtectionCommandFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.SecretProtectionRecoveryKeyUnreadable,
        Level = LogLevel.Error,
        Message = "The recovery private key file '{Path}' cannot be read, or it is empty or too large."
    )]
    internal static partial void SecretProtectionRecoveryKeyUnreadable(
        this ILogger logger,
        string path,
        Exception? exception
    );

    [LoggerMessage(
        EventId = EventIds.SecretProtectionCommandOwnerHasNoMasterKeys,
        Level = LogLevel.Error,
        Message = "The owner '{Owner}' that --owner names has no master keys; nothing was rewrapped."
    )]
    internal static partial void SecretProtectionCommandOwnerHasNoMasterKeys(
        this ILogger logger,
        SecretOwner owner
    );

    [LoggerMessage(
        EventId = EventIds.SecretProtectionCommandUsageInvalid,
        Level = LogLevel.Error,
        Message = "The secret-protection command line is invalid: {Error} {Usage}"
    )]
    internal static partial void SecretProtectionCommandUsageInvalid(
        this ILogger logger,
        string error,
        string usage
    );

    [LoggerMessage(
        EventId = EventIds.DeveloperPlatformMasterKeysReplaced,
        Level = LogLevel.Warning,
        Message = "The platform's master keys cannot be unwrapped, so developer key pruning removed them and revoked every Data Protection key they protected; the platform starts over with a new master key"
    )]
    internal static partial void DeveloperPlatformMasterKeysReplaced(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.DeveloperBffClientReplaced,
        Level = LogLevel.Information,
        Message = "Replaced the stored developer BFF client '{ClientId}' with its configuration, because the stored client differs from it"
    )]
    internal static partial void DeveloperBffClientReplaced(this ILogger logger, string clientId);

    [LoggerMessage(
        EventId = EventIds.DeveloperBffClientSkipped,
        Level = LogLevel.Warning,
        Message = "The developer BFF client '{ClientId}' is not seeded because its configuration is incomplete; set {MissingKeys}"
    )]
    internal static partial void DeveloperBffClientSkipped(
        this ILogger logger,
        string clientId,
        string missingKeys
    );
}
