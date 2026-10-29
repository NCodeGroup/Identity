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

using JetBrains.Annotations;

namespace NCode.Identity.Bff;

/// <summary>
/// The known event source and event names of the audit events that the <c>Backend for Frontend (BFF)</c> raises.
/// </summary>
/// <remarks>
/// The values are an open vocabulary: a host may raise other events, so a value that is not listed here is not
/// invalid.
/// </remarks>
[PublicAPI]
public static class BffAuditEventNames
{
    /// <summary>
    /// The event source of every audit event that the BFF raises.
    /// </summary>
    public const string Source = "bff";

    /// <summary>
    /// A user logged in and a session was created.
    /// </summary>
    public const string LoginSucceeded = "bff.login.succeeded";

    /// <summary>
    /// A login attempt failed.
    /// </summary>
    public const string LoginFailed = "bff.login.failed";

    /// <summary>
    /// A session ended.
    /// </summary>
    public const string SessionEnded = "bff.session.ended";

    /// <summary>
    /// Refreshing the tokens of a session failed.
    /// </summary>
    public const string TokenRefreshFailed = "bff.token.refresh_failed";

    /// <summary>
    /// A back-channel logout token was accepted.
    /// </summary>
    public const string BackchannelLogoutReceived = "bff.backchannel_logout.received";

    /// <summary>
    /// A back-channel logout token was rejected.
    /// </summary>
    public const string BackchannelLogoutRejected = "bff.backchannel_logout.rejected";

    /// <summary>
    /// A request was refused by the anti-forgery checks.
    /// </summary>
    public const string AntiforgeryRejected = "bff.antiforgery.rejected";
}
