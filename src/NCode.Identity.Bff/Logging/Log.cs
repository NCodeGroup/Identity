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
        Message = "The BFF endpoint check failed after the endpoints or the frontends changed: {Failure}"
    )]
    internal static partial void EndpointCheckFailed(this ILogger logger, string failure);

    [LoggerMessage(
        EventId = EventIds.FrontendsChanged,
        Level = LogLevel.Information,
        Message = "The BFF frontends changed: {Frontends}"
    )]
    internal static partial void FrontendsChanged(this ILogger logger, string frontends);

    [LoggerMessage(
        EventId = EventIds.FrontendsChangeRejected,
        Level = LogLevel.Error,
        Message = "The BFF kept its frontends because the changed configuration is invalid: {Failures}"
    )]
    internal static partial void FrontendsChangeRejected(this ILogger logger, string failures);

    [LoggerMessage(
        EventId = EventIds.FrontendSessionsEnded,
        Level = LogLevel.Information,
        Message = "The BFF ended {Count} sessions of the frontend {Frontend}: {Reason}"
    )]
    internal static partial void FrontendSessionsEnded(
        this ILogger logger,
        int count,
        string frontend,
        string reason
    );

    [LoggerMessage(
        EventId = EventIds.FrontendRetirementFailed,
        Level = LogLevel.Warning,
        Message = "The BFF could not end the sessions of a removed or changed frontend and tries again"
    )]
    internal static partial void FrontendRetirementFailed(this ILogger logger, Exception exception);
}
