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

namespace NCode.Identity.OpenId.Authentication.Sessions;

/// <summary>
/// The well-known reasons an end-user session is terminated. Carried on <see cref="OnSessionTerminatedCommand"/> so the
/// side-effect handlers (grant revocation, back-channel logout, auditing) and any consumer handlers can react to the
/// cause.
/// </summary>
/// <remarks>
/// The reasons are an open vocabulary (ADR-0150): a consumer or an extension that ends a session for its own cause, such
/// as a risk signal or a password change, passes its own name. Names are compared ordinally.
/// </remarks>
[PublicAPI]
public static class SessionTerminationReasons
{
    /// <summary>
    /// The session was terminated by an RP-Initiated Logout at the <c>end_session_endpoint</c>.
    /// </summary>
    public const string Logout = "logout";

    /// <summary>
    /// The session expired because it was idle longer than its sliding inactivity window.
    /// </summary>
    public const string IdleTimeout = "idle_timeout";

    /// <summary>
    /// The session reached its absolute lifetime and expired.
    /// </summary>
    public const string AbsoluteExpiry = "absolute_expiry";

    /// <summary>
    /// The end-user's account was deleted, so every session of its principal was terminated.
    /// </summary>
    public const string AccountDeleted = "account_deleted";

    /// <summary>
    /// The end-user's account was disabled, so every session of its principal was terminated.
    /// </summary>
    public const string AccountDisabled = "account_disabled";

    /// <summary>
    /// The session was evicted to keep the subject within the concurrent-session limit.
    /// </summary>
    public const string ConcurrentSessionLimit = "concurrent_session_limit";

    /// <summary>
    /// The session was terminated administratively (for example, from a management surface).
    /// </summary>
    public const string AdministrativeRevocation = "administrative_revocation";

    /// <summary>
    /// Every session the subject holds was terminated ("sign out everywhere").
    /// </summary>
    public const string GlobalSignOut = "global_sign_out";

    /// <summary>
    /// The end-user terminated the session themselves from a self-service active-sessions surface (for example,
    /// signing out another device, or "sign out everywhere else").
    /// </summary>
    public const string UserInitiated = "user_initiated";
}
