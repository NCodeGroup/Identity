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
/// The known reasons that a BFF session ended.
/// </summary>
/// <remarks>
/// The reasons are an open vocabulary: a session may end for a reason that is not listed here. Reasons are compared
/// ordinally.
/// </remarks>
[PublicAPI]
public static class BffSessionEndReasons
{
    /// <summary>
    /// The user logged out.
    /// </summary>
    public const string LoggedOut = "logged_out";

    /// <summary>
    /// The session was idle for longer than the idle timeout.
    /// </summary>
    public const string IdleTimeout = "idle_timeout";

    /// <summary>
    /// The session reached its absolute expiry.
    /// </summary>
    public const string AbsoluteExpiry = "absolute_expiry";

    /// <summary>
    /// The refresh token was rejected or is missing.
    /// </summary>
    public const string RefreshFailed = "refresh_failed";

    /// <summary>
    /// The provider ended the session with a back-channel logout token.
    /// </summary>
    public const string BackchannelLogout = "backchannel_logout";

    /// <summary>
    /// A new login replaced the session.
    /// </summary>
    public const string Replaced = "replaced";

    /// <summary>
    /// A session validator ended the session.
    /// </summary>
    public const string EndedByValidator = "ended_by_validator";
}
