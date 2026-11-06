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

    [LoggerMessage(
        EventId = EventIds.RemoteApiForwardFailed,
        Level = LogLevel.Warning,
        Message = "The BFF could not forward a request to the remote API at {Path}: {Error}"
    )]
    internal static partial void RemoteApiForwardFailed(
        this ILogger logger,
        string path,
        string error,
        Exception? exception
    );

    [LoggerMessage(
        EventId = EventIds.EndpointCheckFailed,
        Level = LogLevel.Error,
        Message = "The BFF endpoint check failed after the endpoints changed: {Failure}"
    )]
    internal static partial void EndpointCheckFailed(this ILogger logger, string failure);

    [LoggerMessage(
        EventId = EventIds.ConnectionRevalidationFailed,
        Level = LogLevel.Warning,
        Message = "The BFF could not revalidate the session that a connection holds because the session store is unavailable; the connection keeps its session"
    )]
    internal static partial void ConnectionRevalidationFailed(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.ConnectionRevalidationFaulted,
        Level = LogLevel.Error,
        Message = "Revalidating the BFF session that a connection holds failed; the connection ends"
    )]
    internal static partial void ConnectionRevalidationFaulted(
        this ILogger logger,
        Exception exception
    );

    [LoggerMessage(
        EventId = EventIds.HubRevalidationFaulted,
        Level = LogLevel.Error,
        Message = "Waiting for the BFF session of a hub connection to end failed; the connection is aborted"
    )]
    internal static partial void HubRevalidationFaulted(this ILogger logger, Exception exception);
}
