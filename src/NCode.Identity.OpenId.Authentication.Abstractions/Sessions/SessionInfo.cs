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
/// Describes a single active end-user session when enumerating the sessions a subject holds (for example, the
/// "active sessions" management/account surface). It couples the session's opaque <c>sid</c> and server-side lifecycle
/// timestamps with the authentication context captured at sign-in.
/// </summary>
/// <param name="SessionId">The opaque session identifier (<c>sid</c>).</param>
/// <param name="SubjectId">The stable, server-owned principal identifier the session belongs to.</param>
/// <param name="CreatedWhen">The time the session was established.</param>
/// <param name="ExpiresWhen">The time the session ends unless it is used again: the end of its idle window or, if
/// earlier, its absolute end.</param>
/// <param name="Grant">The authentication context and relying-party list captured for the session.</param>
[PublicAPI]
public readonly record struct SessionInfo(
    string SessionId,
    string? SubjectId,
    DateTimeOffset CreatedWhen,
    DateTimeOffset ExpiresWhen,
    SessionGrant Grant
);
