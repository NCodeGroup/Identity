// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Logging;

namespace NCode.Identity.Bff.Logging;

/// <summary>
/// Provides source-generated, strongly-typed log messages for the <c>NCode.Identity.Bff</c> package.
/// </summary>
[ExcludeFromCodeCoverage]
internal static partial class Log
{
    [LoggerMessage(
        EventId = EventIds.LoginFailed,
        Level = LogLevel.Debug,
        Message = "The BFF login failed: {Reason}"
    )]
    internal static partial void LoginFailed(this ILogger logger, string reason);

    [LoggerMessage(
        EventId = EventIds.RefreshFailed,
        Level = LogLevel.Warning,
        Message = "The BFF token refresh failed: {Reason}"
    )]
    internal static partial void RefreshFailed(
        this ILogger logger,
        string reason,
        Exception? exception
    );

    [LoggerMessage(
        EventId = EventIds.RefreshTokenRevocationFailed,
        Level = LogLevel.Warning,
        Message = "The BFF could not revoke the refresh token at logout: {Reason}"
    )]
    internal static partial void RefreshTokenRevocationFailed(
        this ILogger logger,
        string reason,
        Exception? exception
    );

    [LoggerMessage(
        EventId = EventIds.RefreshLeaseReleaseFailed,
        Level = LogLevel.Warning,
        Message = "The BFF could not release a refresh lease; the lease expires on its own"
    )]
    internal static partial void RefreshLeaseReleaseFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.TokenRequestFailed,
        Level = LogLevel.Warning,
        Message = "The BFF {GrantType} token request failed: {Reason}"
    )]
    internal static partial void TokenRequestFailed(
        this ILogger logger,
        string grantType,
        string reason,
        Exception? exception
    );
}
