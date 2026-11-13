// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.OpenId.Setup.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.OpenId.Setup</c> package: the
/// generated setup token an operator reads once, the replica that issued it, and the root administrator's creation.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.SetupTokenIssued,
        Level = LogLevel.Warning,
        Message = "Open /setup on this instance and enter the setup token {SetupToken}; it expires at {ExpiresAt}."
    )]
    internal static partial void SetupTokenIssued(
        this ILogger logger,
        string setupToken,
        DateTimeOffset expiresAt
    );

    [LoggerMessage(
        EventId = EventIds.SetupTokenIssuedByAnotherReplica,
        Level = LogLevel.Information,
        Message = "Another replica issued the setup token at {IssuedAt}, so this replica did not issue one."
    )]
    internal static partial void SetupTokenIssuedByAnotherReplica(
        this ILogger logger,
        DateTimeOffset? issuedAt
    );

    [LoggerMessage(
        EventId = EventIds.RootAdministratorCreated,
        Level = LogLevel.Information,
        Message = "Created the root administrator {PrincipalId} in tenant {TenantId} and marked the instance initialized."
    )]
    internal static partial void RootAdministratorCreated(
        this ILogger logger,
        string tenantId,
        string principalId
    );
}
