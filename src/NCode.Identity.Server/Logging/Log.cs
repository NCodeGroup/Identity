// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

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
}
