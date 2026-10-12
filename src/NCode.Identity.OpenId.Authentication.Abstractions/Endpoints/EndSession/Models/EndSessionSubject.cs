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

namespace NCode.Identity.OpenId.Authentication.Endpoints.EndSession.Models;

/// <summary>
/// Identifies the end-user whose session is being terminated by an RP-Initiated Logout, resolved from the
/// <c>id_token_hint</c> or the current OP login cookie.
/// </summary>
/// <param name="SubjectId">The identifier of the subject being logged out, or <see langword="null"/> when it could not
/// be resolved.</param>
/// <param name="SessionId">The opaque session identifier (<c>sid</c>) being terminated, or <see langword="null"/> when
/// it could not be resolved.</param>
/// <param name="ClientId">The identifier of the relying party that requested the logout, or <see langword="null"/>.</param>
/// <param name="ParticipatingClientIds">The relying parties that participated in the session and are therefore
/// candidates for back-channel logout notification.</param>
[PublicAPI]
public readonly record struct EndSessionSubject(
    string? SubjectId,
    string? SessionId,
    string? ClientId,
    IReadOnlyList<string> ParticipatingClientIds
);
