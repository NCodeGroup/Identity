#region Copyright Preamble

// Copyright @ 2026 NCode Group
//
//    Licensed under the Apache License, Version 2.0 (the "License");
//    you may not use this file except in compliance with the License.
//    You may obtain a copy of the License at
//
//        http://www.apache.org/licenses/LICENSE-2.0
//
//    Unless required by applicable law or agreed to in writing, software
//    distributed under the License is distributed on an "AS IS" BASIS,
//    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//    See the License for the specific language governing permissions and
//    limitations under the License.

#endregion

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
