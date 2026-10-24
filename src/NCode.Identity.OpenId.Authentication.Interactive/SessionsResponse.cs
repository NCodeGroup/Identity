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

namespace NCode.Identity.OpenId.Authentication.Interactive;

/// <summary>
/// Describes one active session of the end-user, for the active-sessions page.
/// </summary>
/// <param name="SessionId">The opaque session identifier (<c>sid</c>).</param>
/// <param name="IsCurrent">Whether this is the session of the request making the call, so the page can mark it and
/// exclude it from "sign out everywhere else".</param>
/// <param name="CreatedWhen">The time the session was established, as a Unix timestamp in seconds.</param>
/// <param name="LastActivityWhen">The most recent time the session was observed in use, as a Unix timestamp in
/// seconds.</param>
/// <param name="ExpiresWhen">The time the session ends unless it is used again, as a Unix timestamp in seconds: the end
/// of its idle window or, if earlier, its absolute end.</param>
/// <param name="Amr">The authentication method references (<c>amr</c>) captured at sign-in.</param>
/// <param name="Idp">The identity provider (<c>idp</c>) that authenticated the end-user, or <see langword="null"/>.</param>
/// <param name="UserAgent">The <c>User-Agent</c> captured at sign-in, or <see langword="null"/> when none was recorded.</param>
/// <param name="IpAddress">The remote IP address captured at sign-in, or <see langword="null"/> when none was recorded.</param>
internal sealed record SessionSummaryResponse(
    string SessionId,
    bool IsCurrent,
    long CreatedWhen,
    long LastActivityWhen,
    long ExpiresWhen,
    IReadOnlyList<string> Amr,
    string? Idp,
    string? UserAgent,
    string? IpAddress
);

/// <summary>
/// The JSON payload the active-sessions page fetches to render the end-user's active sessions.
/// </summary>
/// <param name="Sessions">The end-user's active sessions, newest first.</param>
internal sealed record SessionsResponse(IReadOnlyList<SessionSummaryResponse> Sessions);
