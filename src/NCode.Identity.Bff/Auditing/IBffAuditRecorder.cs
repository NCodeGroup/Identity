// Copyright © NCode Group
// SPDX-License-Identifier: Apache-2.0

using Microsoft.AspNetCore.Http;

namespace NCode.Identity.Bff.Auditing;

/// <summary>
/// Records the audit events of the <c>Backend for Frontend (BFF)</c> through the event seam.
/// </summary>
internal interface IBffAuditRecorder
{
    /// <summary>
    /// Records that a user logged in and a session was created.
    /// </summary>
    ValueTask LoginSucceededAsync(
        HttpContext httpContext,
        string subject,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that a login attempt failed.
    /// </summary>
    ValueTask LoginFailedAsync(
        HttpContext httpContext,
        string reason,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that a session ended.
    /// </summary>
    ValueTask SessionEndedAsync(
        HttpContext? httpContext,
        string subject,
        string reason,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that refreshing the tokens of a session failed.
    /// </summary>
    ValueTask TokenRefreshFailedAsync(
        HttpContext httpContext,
        string subject,
        string reason,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that a back-channel logout token was accepted.
    /// </summary>
    ValueTask BackchannelLogoutReceivedAsync(
        HttpContext httpContext,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that a back-channel logout token was rejected.
    /// </summary>
    ValueTask BackchannelLogoutRejectedAsync(
        HttpContext httpContext,
        string reason,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Records that a request was refused by the anti-forgery checks.
    /// </summary>
    ValueTask AntiforgeryRejectedAsync(
        HttpContext httpContext,
        string reason,
        CancellationToken cancellationToken
    );
}
