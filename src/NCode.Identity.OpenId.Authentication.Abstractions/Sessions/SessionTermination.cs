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
/// Describes a single end-user session termination: its cause, the subject and session scope, and the relying parties
/// that participated in the session (the back-channel logout notification list). A <see cref="SessionId"/> of
/// <see langword="null"/> denotes a subject-wide termination (for example, "sign out everywhere").
/// </summary>
/// <param name="Reason">Why the session is being terminated.</param>
/// <param name="SubjectId">The stable, server-owned principal identifier whose session is terminated, when known.</param>
/// <param name="SessionId">The opaque session identifier (<c>sid</c>) being terminated, or <see langword="null"/> for a
/// subject-wide termination.</param>
/// <param name="ParticipatingClientIds">The relying parties that participated in the terminated session(s) and are
/// therefore candidates for back-channel logout notification.</param>
[PublicAPI]
public readonly record struct SessionTermination(
    SessionTerminationReason Reason,
    string? SubjectId,
    string? SessionId,
    IReadOnlyList<string> ParticipatingClientIds
);
