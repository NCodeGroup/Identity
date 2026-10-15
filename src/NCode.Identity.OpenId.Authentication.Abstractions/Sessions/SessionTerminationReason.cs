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
/// Identifies why an end-user session was terminated. Carried on <see cref="OnSessionTerminatedCommand"/> so the
/// side-effect handlers (grant revocation, back-channel logout, auditing) and any consumer handlers can react to the
/// cause.
/// </summary>
[PublicAPI]
public enum SessionTerminationReason
{
    /// <summary>
    /// The session was terminated by an RP-Initiated Logout at the <c>end_session_endpoint</c>.
    /// </summary>
    Logout = 0,

    /// <summary>
    /// The session expired because it was idle longer than its sliding inactivity window.
    /// </summary>
    IdleTimeout = 1,

    /// <summary>
    /// The session reached its absolute lifetime and expired.
    /// </summary>
    AbsoluteExpiry = 2,

    /// <summary>
    /// The session was evicted to keep the subject within the concurrent-session limit.
    /// </summary>
    ConcurrentSessionLimit = 3,

    /// <summary>
    /// The session was terminated administratively (for example, from a management surface).
    /// </summary>
    AdministrativeRevocation = 4,

    /// <summary>
    /// Every session the subject holds was terminated ("sign out everywhere").
    /// </summary>
    GlobalSignOut = 5,
}
