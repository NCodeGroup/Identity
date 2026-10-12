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
/// The JSON payload the account ("me") page fetches to render the current end-user's session.
/// </summary>
/// <param name="Subject">The subject identifier (<c>sub</c>) of the signed-in end-user.</param>
/// <param name="Name">A human-readable display name (the <c>name</c> or <c>preferred_username</c> claim), or the
/// subject identifier when neither is present.</param>
/// <param name="AuthTime">The time of authentication as a Unix timestamp in seconds, or <see langword="null"/> when
/// unknown.</param>
/// <param name="Amr">The authentication method references (<c>amr</c>) of the session.</param>
/// <param name="Idp">The identity provider (<c>idp</c>) that authenticated the end-user, or <see langword="null"/>.</param>
/// <param name="SessionId">The opaque server-side session identifier (<c>sid</c>), or <see langword="null"/>.</param>
internal sealed record AccountMeResponse(
    string Subject,
    string? Name,
    long? AuthTime,
    IReadOnlyList<string> Amr,
    string? Idp,
    string? SessionId
);
